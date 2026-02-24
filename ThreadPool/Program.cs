using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThreadPoolLab;

namespace ThreadPoolLab
{
    class Program
    {
        private const int RandomSeed = 345;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== Система обработки финансовых данных ===");
            Console.WriteLine("Введите количество элементов (0 для выхода):\n");

            while (true)
            {
                // ── Ввод размера данных ───────────────────────────────────────
                long dataSize = ReadDataSize();
                if (dataSize == 0)
                {
                    Console.WriteLine("Выход из программы.");
                    break;
                }

                await RunProcessingAsync(dataSize);

                Console.WriteLine();
                Console.WriteLine("Введите количество элементов (0 для выхода):");
            }
        }

        /// <summary>
        /// Считывает и валидирует введённое пользователем число.
        /// Принимает числа с пробелами и подчёркиваниями (1 000 000 / 1_000_000).
        /// Повторяет запрос при некорректном вводе.
        /// </summary>
        private static long ReadDataSize()
        {
            while (true)
            {
                Console.Write("> ");
                string? raw = Console.ReadLine();

                if (raw == null)
                    return 0;

                // Убираем пробелы, подчёркивания и разделители тысяч
                string cleaned = raw
                    .Replace(" ", "")
                    .Replace("_", "")
                    .Replace(",", "")
                    .Replace(".", "")
                    .Trim();

                if (long.TryParse(cleaned, out long value) && value >= 0)
                    return value;

                Console.WriteLine("  Некорректный ввод. Введите целое неотрицательное число (0 для выхода).");
            }
        }

        /// <summary>
        /// Выполняет полный цикл генерации данных, обработки всеми методами
        /// и вывода сводной статистики для заданного размера.
        /// </summary>
        private static async Task RunProcessingAsync(long dataSize)
        {
            Console.WriteLine($"\nГенерация {dataSize:N0} элементов...");

            // ── Генерация данных ──────────────────────────────────────────────
            decimal[] data = GenerateData(dataSize, RandomSeed);

            await AsyncLogger.LogAsync($"Запуск обработки. Размер данных: {dataSize:N0}");

            // Новый процессор на каждую итерацию — явная изоляция ресурсов
            var processor = new TaskProcessor();

            // ── Последовательная обработка ────────────────────────────────────
            Console.WriteLine("Последовательная обработка...");
            var sw = Stopwatch.StartNew();
            decimal seqSum = SequentialSum(data);
            sw.Stop();
            long seqMs = sw.ElapsedMilliseconds;
            await AsyncLogger.LogAsync($"Последовательная обработка: {seqMs} мс, сумма={seqSum}");
            Console.WriteLine($"  Готово за {seqMs} мс");

            // ── ThreadPool ────────────────────────────────────────────────────
            Console.WriteLine("ThreadPool обработка...");
            sw.Restart();
            decimal[] tpParts = processor.ProcessDataWithThreadPool(data);
            decimal tpSum = tpParts.Sum();
            sw.Stop();
            long tpMs = sw.ElapsedMilliseconds;
            await AsyncLogger.LogAsync($"ThreadPool: {tpMs} мс, сумма={tpSum}");
            Console.WriteLine($"  Готово за {tpMs} мс");

            // ── TAP ───────────────────────────────────────────────────────────
            Console.WriteLine("TAP обработка...");
            sw.Restart();
            decimal[] tapParts = await processor.ProcessDataAsync(data);
            decimal tapSum = tapParts.Sum();
            sw.Stop();
            long tapMs = sw.ElapsedMilliseconds;
            await AsyncLogger.LogAsync($"TAP: {tapMs} мс, сумма={tapSum}");
            Console.WriteLine($"  Готово за {tapMs} мс");

            // ── APM ───────────────────────────────────────────────────────────
            Console.WriteLine("APM обработка...");

            using var apmDone = new ManualResetEventSlim(false);
            decimal[] apmParts = [];
            Exception? apmError = null;

            sw.Restart();
            AsyncLogger.LogWithCallback("APM обработка начата", null);

            processor.BeginProcessData(data, ar =>
            {
                try
                {
                    apmParts = processor.EndProcessData(ar);
                }
                catch (Exception ex)
                {
                    apmError = ex;
                }
                finally
                {
                    apmDone.Set();
                }
            }, null);

            apmDone.Wait();
            sw.Stop();
            long apmMs = sw.ElapsedMilliseconds;

            if (apmError != null)
                throw apmError;

            decimal apmSum = apmParts.Sum();
            await AsyncLogger.LogAsync($"APM: {apmMs} мс, сумма={apmSum}");
            Console.WriteLine($"  Готово за {apmMs} мс");

            // ── Проверка корректности ─────────────────────────────────────────
            const decimal tolerance = 0.0001m;
            bool allMatch =
                Math.Abs(seqSum - tpSum)  < tolerance &&
                Math.Abs(seqSum - tapSum) < tolerance &&
                Math.Abs(seqSum - apmSum) < tolerance;

            // ── Коэффициенты ускорения ────────────────────────────────────────
            double tpSpeedup  = seqMs > 0 ? (double)seqMs / tpMs  : 0;
            double tapSpeedup = seqMs > 0 ? (double)seqMs / tapMs : 0;
            double apmSpeedup = seqMs > 0 ? (double)seqMs / apmMs : 0;

            // ── Сводная статистика ────────────────────────────────────────────
            Console.WriteLine(new string('═', 50));
            Console.WriteLine("=== Результаты обработки ===");
            Console.WriteLine($"Размер данных:           {dataSize,14:N0} элементов");
            Console.WriteLine($"Последовательная:        {seqMs,8} мс");
            Console.WriteLine($"ThreadPool обработка:    {tpMs,8} мс");
            Console.WriteLine($"TAP обработка:           {tapMs,8} мс");
            Console.WriteLine($"APM обработка:           {apmMs,8} мс");
            Console.WriteLine($"Ускорение ThreadPool:    {tpSpeedup,8:F2}x");
            Console.WriteLine($"Ускорение TAP:           {tapSpeedup,8:F2}x");
            Console.WriteLine($"Ускорение APM:           {apmSpeedup,8:F2}x");
            Console.WriteLine($"Результаты совпадают:    {(allMatch ? "Да" : "Нет")}");
            Console.WriteLine(new string('═', 50));

            await AsyncLogger.LogAsync(
                $"Итог — Seq:{seqMs}мс TP:{tpMs}мс TAP:{tapMs}мс APM:{apmMs}мс " +
                $"Совпадение:{(allMatch ? "Да" : "Нет")}");

            // ── Явная очистка памяти ──────────────────────────────────────────
            // Подсказываем GC собрать массив, который мог занять сотни МБ
            data = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static decimal[] GenerateData(long size, int seed)
        {
            var rng = new Random(seed);
            var result = new decimal[size];
            for (int i = 0; i < size; i++)
                result[i] = (decimal)(rng.NextDouble() * 999.0 + 1.0);
            return result;
        }

        private static decimal SequentialSum(decimal[] data)
        {
            decimal sum = 0m;
            for (int i = 0; i < data.Length; i++)
                sum += data[i];
            return sum;
        }
    }
}