using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SynchronizationLab
{
    /// <summary>
    /// Бенчмарк для сравнения производительности различных подходов к синхронизации.
    /// </summary>
    public class SynchronizationBenchmark
    {
        private readonly TransactionProcessor _processor = new TransactionProcessor();

        /// <summary>
        /// Измеряет производительность без синхронизации.
        /// Возвращает (время_мс, итоговый_баланс, корректность).
        /// </summary>
        public (long timeMs, decimal balance, bool isCorrect) BenchmarkNoSync(
            BankAccount account, List<decimal> transactions, decimal expectedBalance)
        {
            var sw = Stopwatch.StartNew();
            decimal result = _processor.ProcessTransactionsConcurrently(account, transactions);
            sw.Stop();

            bool isCorrect = result == expectedBalance;
            return (sw.ElapsedMilliseconds, result, isCorrect);
        }

        /// <summary>
        /// Измеряет производительность с использованием lock.
        /// Возвращает (время_мс, итоговый_баланс, корректность).
        /// </summary>
        public (long timeMs, decimal balance, bool isCorrect) BenchmarkWithLock(
            BankAccount account, List<decimal> transactions, decimal expectedBalance)
        {
            var sw = Stopwatch.StartNew();
            decimal result = _processor.ProcessTransactionsWithLock(account, transactions);
            sw.Stop();

            bool isCorrect = result == expectedBalance;
            return (sw.ElapsedMilliseconds, result, isCorrect);
        }

        /// <summary>
        /// Измеряет производительность с использованием Monitor.
        /// Возвращает (время_мс, итоговый_баланс, корректность).
        /// </summary>
        public (long timeMs, decimal balance, bool isCorrect) BenchmarkWithMonitor(
            BankAccount account, List<decimal> transactions, decimal expectedBalance)
        {
            var sw = Stopwatch.StartNew();
            decimal result = _processor.ProcessTransactionsWithMonitor(account, transactions);
            sw.Stop();

            bool isCorrect = result == expectedBalance;
            return (sw.ElapsedMilliseconds, result, isCorrect);
        }

        /// <summary>
        /// Выполняет полное сравнение всех трёх подходов и выводит результаты.
        /// </summary>
        public void CompareAllApproaches()
        {
            Console.WriteLine("\n=== Сравнение подходов к синхронизации ===\n");

            const int transactionCount = 1000;
            const decimal initialBalance = 100_000m;
            var random = new Random(42);

            // Генерация транзакций: 500 депозитов, 500 снятий
            var transactions = new List<decimal>();
            for (int i = 0; i < transactionCount / 2; i++)
            {
                decimal depositAmount = Math.Round((decimal)(random.NextDouble() * 990 + 10), 2);
                transactions.Add(depositAmount);
            }
            for (int i = 0; i < transactionCount / 2; i++)
            {
                decimal withdrawAmount = Math.Round((decimal)(random.NextDouble() * 990 + 10), 2);
                transactions.Add(-withdrawAmount);
            }

            // Ожидаемый баланс при последовательном выполнении
            decimal expectedBalance = initialBalance;
            foreach (var tx in transactions)
            {
                if (tx >= 0)
                    expectedBalance += tx;
                else
                    expectedBalance += tx; // tx уже отрицательный
            }

            // --- Бенчмарк без синхронизации ---
            var accountNoSync = new BankAccount("NoSync", initialBalance);
            var resultNoSync = BenchmarkNoSync(accountNoSync, transactions, expectedBalance);

            // --- Бенчмарк с lock ---
            var accountLock = new BankAccount("Lock", initialBalance);
            var resultLock = BenchmarkWithLock(accountLock, transactions, expectedBalance);

            // --- Бенчмарк с Monitor ---
            var accountMonitor = new BankAccount("Monitor", initialBalance);
            var resultMonitor = BenchmarkWithMonitor(accountMonitor, transactions, expectedBalance);

            // Вывод результатов
            Console.WriteLine($"Без синхронизации: {resultNoSync.timeMs} мс, результат: {(resultNoSync.isCorrect ? "корректный" : "некорректный")}");
            Console.WriteLine($"  Баланс: {resultNoSync.balance:F2}, ожидаемый: {expectedBalance:F2}");
            Console.WriteLine();

            Console.WriteLine($"С использованием lock: {resultLock.timeMs} мс, результат: {(resultLock.isCorrect ? "корректный" : "некорректный")}");
            Console.WriteLine($"  Баланс: {resultLock.balance:F2}, ожидаемый: {expectedBalance:F2}");
            Console.WriteLine();

            Console.WriteLine($"С использованием Monitor: {resultMonitor.timeMs} мс, результат: {(resultMonitor.isCorrect ? "корректный" : "некорректный")}");
            Console.WriteLine($"  Баланс: {resultMonitor.balance:F2}, ожидаемый: {expectedBalance:F2}");
            Console.WriteLine();

            // Сравнение производительности
            if (resultNoSync.timeMs > 0)
            {
                double overheadLock = ((double)resultLock.timeMs - resultNoSync.timeMs) / resultNoSync.timeMs * 100;
                double overheadMonitor = ((double)resultMonitor.timeMs - resultNoSync.timeMs) / resultNoSync.timeMs * 100;
                double lockVsMonitor = resultMonitor.timeMs > 0
                    ? (double)resultLock.timeMs / resultMonitor.timeMs
                    : 0;

                Console.WriteLine("Сравнение производительности:");
                Console.WriteLine($"  Накладные расходы lock: {overheadLock:F1}%");
                Console.WriteLine($"  Накладные расходы Monitor: {overheadMonitor:F1}%");
                Console.WriteLine($"  Соотношение lock/Monitor: {lockVsMonitor:F2}x");
            }
        }

        /// <summary>
        /// Демонстрация влияния количества потоков на производительность.
        /// </summary>
        public void BenchmarkThreadScaling()
        {
            Console.WriteLine("\n=== Влияние количества потоков на производительность ===\n");

            int[] threadCounts = { 10, 50, 100, 500, 1000 };

            Console.WriteLine($"{"Потоков",-10} {"lock (мс)",-12} {"Monitor (мс)",-14} {"lock корр.",-12} {"Monitor корр.",-14}");
            Console.WriteLine(new string('-', 62));

            foreach (int count in threadCounts)
            {
                var random = new Random(42);
                var transactions = new List<decimal>();
                decimal initialBalance = count * 1000m; // достаточно средств

                for (int i = 0; i < count / 2; i++)
                {
                    transactions.Add(Math.Round((decimal)(random.NextDouble() * 990 + 10), 2));
                }
                for (int i = 0; i < count - count / 2; i++)
                {
                    transactions.Add(-Math.Round((decimal)(random.NextDouble() * 990 + 10), 2));
                }

                decimal expected = initialBalance;
                foreach (var tx in transactions)
                    expected += tx;

                // lock
                var accLock = new BankAccount("Lock", initialBalance);
                var resLock = BenchmarkWithLock(accLock, transactions, expected);

                // Monitor
                var accMonitor = new BankAccount("Monitor", initialBalance);
                var resMonitor = BenchmarkWithMonitor(accMonitor, transactions, expected);

                Console.WriteLine($"{count,-10} {resLock.timeMs,-12} {resMonitor.timeMs,-14} {(resLock.isCorrect ? "Да" : "Нет"),-12} {(resMonitor.isCorrect ? "Да" : "Нет"),-14}");
            }
        }
    }
}
