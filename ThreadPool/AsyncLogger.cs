using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolLab
{
    /// <summary>
    /// Асинхронный логгер: записывает сообщения в файл
    /// с использованием TAP и APM шаблонов.
    /// </summary>
    public static class AsyncLogger
    {
        private static readonly string LogFilePath = "processing.log";

        // Объект синхронизации для предотвращения одновременной записи
        private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// TAP: асинхронно записывает сообщение в лог-файл.
        /// Использует FileStream с флагом FileOptions.Asynchronous.
        /// </summary>
        public static async Task LogAsync(string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            byte[] bytes = Encoding.UTF8.GetBytes(line);

            // Захватываем семафор, чтобы не смешивать записи из разных потоков
            await WriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // FileOptions.Asynchronous включает истинно асинхронный I/O на уровне ОС
                await using var fs = new FileStream(
                    LogFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous);

                await fs.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            finally
            {
                WriteLock.Release();
            }
        }

        /// <summary>
        /// APM: записывает сообщение в лог-файл с использованием
        /// BeginWrite/EndWrite. Вызывает callback по завершении.
        /// async/await не используется.
        /// </summary>
        public static void LogWithCallback(string message, Action? callback)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            byte[] bytes = Encoding.UTF8.GetBytes(line);

            // Открываем поток (будет закрыт в EndWrite-callback)
            var fs = new FileStream(
                LogFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);   // useAsync=true эквивалентен FileOptions.Asynchronous

            // Состояние, передаваемое через IAsyncResult.AsyncState
            var state = (Stream: fs, Callback: callback);

            fs.BeginWrite(bytes, 0, bytes.Length, ar =>
            {
                // Извлекаем состояние
                var (stream, cb) = ((FileStream, Action?))ar.AsyncState!;
                try
                {
                    stream.EndWrite(ar);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[AsyncLogger] Ошибка записи: {ex.Message}");
                }
                finally
                {
                    stream.Dispose();
                    // Вызываем пользовательский callback
                    cb?.Invoke();
                }
            }, state);
        }
    }
}
