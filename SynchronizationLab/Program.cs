using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SynchronizationLab
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Практическое задание 3: Синхронизация — Monitor и lock  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝\n");

            // =================================================================
            // 1. Генерация тестовых данных
            // =================================================================
            const int transactionCount = 1000;
            const decimal initialBalance = 500_000m;
            var random = new Random(42); // фиксированный seed для воспроизводимости

            var transactions = new List<decimal>();

            // 500 депозитов
            for (int i = 0; i < 500; i++)
            {
                decimal amount = Math.Round((decimal)(random.NextDouble() * 990 + 10), 2);
                transactions.Add(amount);
            }

            // 500 снятий
            for (int i = 0; i < 500; i++)
            {
                decimal amount = Math.Round((decimal)(random.NextDouble() * 990 + 10), 2);
                transactions.Add(-amount);
            }

            // Ожидаемый баланс при последовательном (корректном) выполнении
            decimal expectedBalance = initialBalance;
            foreach (var tx in transactions)
            {
                expectedBalance += tx; // для положительных — прибавляется, для отрицательных — вычитается
            }

            Console.WriteLine($"Начальный баланс: {initialBalance:F2}");
            Console.WriteLine($"Количество транзакций: {transactionCount}");
            Console.WriteLine($"Ожидаемый итоговый баланс: {expectedBalance:F2}\n");

            var processor = new TransactionProcessor();

            // =================================================================
            // 2. Тестирование без синхронизации
            // =================================================================
            Console.WriteLine("--- Тест 1: Без синхронизации ---");
            var accountNoSync = new BankAccount("Без синхронизации", initialBalance);

            var swNoSync = Stopwatch.StartNew();
            decimal balanceNoSync = processor.ProcessTransactionsConcurrently(accountNoSync, transactions);
            swNoSync.Stop();

            bool correctNoSync = balanceNoSync == expectedBalance;
            Console.WriteLine($"  Итоговый баланс: {balanceNoSync:F2}");
            Console.WriteLine($"  Корректность: {(correctNoSync ? "Да" : "Нет")}");
            Console.WriteLine($"  Гонки данных: {(correctNoSync ? "Нет" : "Да")}");
            Console.WriteLine($"  Время: {swNoSync.ElapsedMilliseconds} мс\n");

            // =================================================================
            // 3. Тестирование с lock
            // =================================================================
            Console.WriteLine("--- Тест 2: С использованием lock ---");
            var accountLock = new BankAccount("Lock", initialBalance);

            var swLock = Stopwatch.StartNew();
            decimal balanceLock = processor.ProcessTransactionsWithLock(accountLock, transactions);
            swLock.Stop();

            bool correctLock = balanceLock == expectedBalance;
            Console.WriteLine($"  Итоговый баланс: {balanceLock:F2}");
            Console.WriteLine($"  Корректность: {(correctLock ? "Да" : "Нет")}");
            Console.WriteLine($"  Время: {swLock.ElapsedMilliseconds} мс\n");

            // =================================================================
            // 4. Тестирование с Monitor
            // =================================================================
            Console.WriteLine("--- Тест 3: С использованием Monitor ---");
            var accountMonitor = new BankAccount("Monitor", initialBalance);

            var swMonitor = Stopwatch.StartNew();
            decimal balanceMonitor = processor.ProcessTransactionsWithMonitor(accountMonitor, transactions);
            swMonitor.Stop();

            bool correctMonitor = balanceMonitor == expectedBalance;
            Console.WriteLine($"  Итоговый баланс: {balanceMonitor:F2}");
            Console.WriteLine($"  Корректность: {(correctMonitor ? "Да" : "Нет")}");
            Console.WriteLine($"  Время: {swMonitor.ElapsedMilliseconds} мс\n");

            // =================================================================
            // 5. Тестирование переводов между счетами
            // =================================================================
            Console.WriteLine("--- Тест 4: Параллельные переводы между счетами ---");
            var accounts = new List<BankAccount>();
            for (int i = 0; i < 10; i++)
            {
                accounts.Add(new BankAccount($"Счёт_{i}", 10_000m));
            }

            decimal totalBefore = 0;
            foreach (var acc in accounts) totalBefore += acc.Balance;
            Console.WriteLine($"  Суммарный баланс до переводов: {totalBefore:F2}");

            processor.ProcessConcurrentTransfers(accounts, 1000);

            decimal totalAfter = 0;
            foreach (var acc in accounts) totalAfter += acc.Balance;
            Console.WriteLine($"  Суммарный баланс после переводов: {totalAfter:F2}");
            Console.WriteLine($"  Баланс сохранён: {(totalBefore == totalAfter ? "Да" : "Нет")}\n");

            // =================================================================
            // 6. Демонстрация deadlock
            // =================================================================
            var deadlockA = new BankAccount("DeadlockA", 5_000m);
            var deadlockB = new BankAccount("DeadlockB", 5_000m);
            processor.DemonstrateDeadlockScenario(deadlockA, deadlockB);

            // =================================================================
            // 7. Сводная статистика
            // =================================================================
            Console.WriteLine("\n=== Результаты тестирования синхронизации ===");
            Console.WriteLine($"Количество транзакций: {transactionCount}");

            Console.WriteLine($"\nБез синхронизации:");
            Console.WriteLine($"  Время: {swNoSync.ElapsedMilliseconds} мс");
            Console.WriteLine($"  Итоговый баланс: {balanceNoSync:F2}");
            Console.WriteLine($"  Корректность: {(correctNoSync ? "Да" : "Нет")}");
            Console.WriteLine($"  Гонки данных: {(correctNoSync ? "Нет" : "Да")}");

            double overheadLockPct = swNoSync.ElapsedMilliseconds > 0
                ? ((double)swLock.ElapsedMilliseconds - swNoSync.ElapsedMilliseconds) / swNoSync.ElapsedMilliseconds * 100
                : 0;

            Console.WriteLine($"\nС использованием lock:");
            Console.WriteLine($"  Время: {swLock.ElapsedMilliseconds} мс");
            Console.WriteLine($"  Итоговый баланс: {balanceLock:F2}");
            Console.WriteLine($"  Корректность: {(correctLock ? "Да" : "Нет")}");
            Console.WriteLine($"  Накладные расходы: {overheadLockPct:F1}%");

            double overheadMonitorPct = swNoSync.ElapsedMilliseconds > 0
                ? ((double)swMonitor.ElapsedMilliseconds - swNoSync.ElapsedMilliseconds) / swNoSync.ElapsedMilliseconds * 100
                : 0;

            Console.WriteLine($"\nС использованием Monitor:");
            Console.WriteLine($"  Время: {swMonitor.ElapsedMilliseconds} мс");
            Console.WriteLine($"  Итоговый баланс: {balanceMonitor:F2}");
            Console.WriteLine($"  Корректность: {(correctMonitor ? "Да" : "Нет")}");
            Console.WriteLine($"  Накладные расходы: {overheadMonitorPct:F1}%");

            double lockVsMonitor = swMonitor.ElapsedMilliseconds > 0
                ? (double)swLock.ElapsedMilliseconds / swMonitor.ElapsedMilliseconds
                : 0;

            Console.WriteLine($"\nСравнение производительности:");
            Console.WriteLine($"  Ускорение lock vs Monitor: {lockVsMonitor:F2}x");
            Console.WriteLine($"  Накладные расходы lock: {overheadLockPct:F1}%");
            Console.WriteLine($"  Накладные расходы Monitor: {overheadMonitorPct:F1}%");

            // =================================================================
            // 8. Бенчмарк (подробное сравнение)
            // =================================================================
            var benchmark = new SynchronizationBenchmark();
            benchmark.CompareAllApproaches();
            benchmark.BenchmarkThreadScaling();

            Console.WriteLine("\n✓ Все тесты завершены.");
        }
    }
}
