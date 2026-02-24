using System;
using System.Threading;
using System.Threading.Tasks;


namespace ThreadPoolLab
{
    /// <summary>
    /// Обрабатывает массивы финансовых данных с использованием
    /// ThreadPool, TAP и APM асинхронных шаблонов.
    /// </summary>
    public class TaskProcessor
    {
        // Количество частей, на которые делится массив
        private const int PartCount = 8;

        #region Вспомогательные методы

        /// <summary>
        /// Вычисляет сумму элементов в заданном диапазоне массива.
        /// Используется всеми методами обработки.
        /// </summary>
        private static decimal SumRange(decimal[] data, int start, int length)
        {
            decimal sum = 0m;
            int end = start + length;
            for (int i = start; i < end; i++)
                sum += data[i];
            return sum;
        }

        /// <summary>
        /// Разбивает массив на PartCount частей.
        /// Возвращает массив пар (start, length) для каждой части.
        /// </summary>
        private static (int start, int length)[] SplitIntoParts(int totalLength)
        {
            var parts = new (int start, int length)[PartCount];
            int baseSize = totalLength / PartCount;
            int remainder = totalLength % PartCount;
            int offset = 0;

            for (int i = 0; i < PartCount; i++)
            {
                // Первые remainder частей получают на 1 элемент больше
                int size = baseSize + (i < remainder ? 1 : 0);
                parts[i] = (offset, size);
                offset += size;
            }

            return parts;
        }

        #endregion

        #region ThreadPool

        /// <summary>
        /// Обрабатывает данные с использованием ThreadPool.
        /// Массив делится на 8 частей, каждая часть суммируется
        /// в отдельном рабочем потоке из пула.
        /// Возвращает массив частичных сумм по каждой части.
        /// </summary>
        public decimal[] ProcessDataWithThreadPool(decimal[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var parts = SplitIntoParts(data.Length);
            var partialSums = new decimal[PartCount];

            // CountdownEvent: ждём завершения всех PartCount рабочих элементов
            using var countdown = new CountdownEvent(PartCount);

            // Храним исключения из рабочих потоков
            Exception[] errors = new Exception[PartCount];

            for (int i = 0; i < PartCount; i++)
            {
                // Захватываем переменные для замыкания
                int partIndex = i;
                (int start, int length) = parts[i];

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        partialSums[partIndex] = SumRange(data, start, length);
                    }
                    catch (Exception ex)
                    {
                        errors[partIndex] = ex;
                    }
                    finally
                    {
                        // Сигнализируем о завершении независимо от результата
                        countdown.Signal();
                    }
                });
            }

            // Блокируем текущий поток до завершения всех рабочих
            countdown.Wait();

            // Проверяем, были ли ошибки в рабочих потоках
            foreach (var error in errors)
            {
                if (error != null)
                    throw new AggregateException("Ошибка в рабочем потоке ThreadPool", error);
            }

            return partialSums;
        }

        #endregion

        #region TAP (Task-based Asynchronous Pattern)

        /// <summary>
        /// Обрабатывает данные по шаблону TAP.
        /// Каждая из 8 частей запускается через Task.Run,
        /// затем результаты объединяются через Task.WhenAll.
        /// </summary>
        public Task<decimal[]> ProcessDataAsync(decimal[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var parts = SplitIntoParts(data.Length);

            // Создаём Task для каждой части
            var tasks = new Task<decimal>[PartCount];
            for (int i = 0; i < PartCount; i++)
            {
                (int start, int length) = parts[i];

                // Task.Run отправляет работу в ThreadPool под управлением TPL
                tasks[i] = Task.Run(() => SumRange(data, start, length));
            }

            // Когда все задачи завершены — собираем массив результатов
            return Task.WhenAll(tasks);
        }

        #endregion

        #region APM (Asynchronous Programming Model)

        /// <summary>
        /// Обрабатывает данные по шаблону APM.
        /// Используются методы BeginProcessData / EndProcessData.
        /// async/await не используется — чистый APM.
        /// </summary>
        public decimal[] ProcessDataWithAPM(decimal[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            IAsyncResult asyncResult = BeginProcessData(data, null, null);
            return EndProcessData(asyncResult);
        }

        /// <summary>
        /// Начинает асинхронную обработку данных (Begin-часть APM).
        /// Запускает работу в ThreadPool и возвращает IAsyncResult.
        /// </summary>
        public IAsyncResult BeginProcessData(
            decimal[] data,
            AsyncCallback? callback,
            object? state)
        {
            var tcs = new TaskCompletionSource<decimal[]>(state);

            // Запускаем вычисления в ThreadPool через QueueUserWorkItem
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var parts = SplitIntoParts(data.Length);
                    var partialSums = new decimal[PartCount];

                    using var countdown = new CountdownEvent(PartCount);
                    Exception[] errors = new Exception[PartCount];

                    for (int i = 0; i < PartCount; i++)
                    {
                        int partIndex = i;
                        (int start, int length) = parts[i];

                        ThreadPool.QueueUserWorkItem(__ =>
                        {
                            try
                            {
                                partialSums[partIndex] = SumRange(data, start, length);
                            }
                            catch (Exception ex)
                            {
                                errors[partIndex] = ex;
                            }
                            finally
                            {
                                countdown.Signal();
                            }
                        });
                    }

                    countdown.Wait();

                    // Проверяем ошибки вложенных рабочих потоков
                    Exception? firstError = null;
                    foreach (var e in errors)
                    {
                        if (e != null) { firstError = e; break; }
                    }

                    if (firstError != null)
                        tcs.SetException(firstError);
                    else
                        tcs.SetResult(partialSums);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    // Вызываем callback, если он передан
                    callback?.Invoke(tcs.Task);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Завершает асинхронную обработку данных (End-часть APM).
        /// Блокирует вызывающий поток до получения результата.
        /// </summary>
        public decimal[] EndProcessData(IAsyncResult asyncResult)
        {
            if (asyncResult is not Task<decimal[]> task)
                throw new ArgumentException("Некорректный IAsyncResult", nameof(asyncResult));

            // Ожидаем завершения и возвращаем результат (блокирующий вызов)
            try
            {
                return task.GetAwaiter().GetResult();
            }
            catch (AggregateException ae)
            {
                throw ae.InnerException ?? ae;
            }
        }

        #endregion
    }
}
