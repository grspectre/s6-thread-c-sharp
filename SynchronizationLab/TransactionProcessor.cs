using System;
using System.Collections.Generic;
using System.Threading;

namespace SynchronizationLab
{
    /// <summary>
    /// Обработчик транзакций — запускает конкурентные операции над счетами.
    /// </summary>
    public class TransactionProcessor
    {
        /// <summary>
        /// Обработка транзакций конкурентно БЕЗ синхронизации.
        /// Положительные суммы — депозиты, отрицательные — снятия.
        /// </summary>
        public decimal ProcessTransactionsConcurrently(BankAccount account, List<decimal> transactions)
        {
            var threads = new List<Thread>();

            foreach (decimal tx in transactions)
            {
                decimal amount = tx; // замыкание
                var thread = new Thread(() =>
                {
                    if (amount >= 0)
                        account.Deposit(amount);
                    else
                        account.Withdraw(Math.Abs(amount));
                });
                threads.Add(thread);
            }

            // Запуск всех потоков
            foreach (var t in threads)
                t.Start();

            // Ожидание завершения
            foreach (var t in threads)
                t.Join();

            return account.Balance;
        }

        /// <summary>
        /// Обработка транзакций конкурентно с использованием lock.
        /// </summary>
        public decimal ProcessTransactionsWithLock(BankAccount account, List<decimal> transactions)
        {
            var threads = new List<Thread>();

            foreach (decimal tx in transactions)
            {
                decimal amount = tx;
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (amount >= 0)
                            account.DepositWithLock(amount);
                        else
                            account.WithdrawWithLock(Math.Abs(amount));
                    }
                    catch (InvalidOperationException)
                    {
                        // Недостаточно средств — допустимая ситуация
                    }
                });
                threads.Add(thread);
            }

            foreach (var t in threads)
                t.Start();

            foreach (var t in threads)
                t.Join();

            return account.Balance;
        }

        /// <summary>
        /// Обработка транзакций конкурентно с использованием Monitor.
        /// </summary>
        public decimal ProcessTransactionsWithMonitor(BankAccount account, List<decimal> transactions)
        {
            var threads = new List<Thread>();

            foreach (decimal tx in transactions)
            {
                decimal amount = tx;
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (amount >= 0)
                            account.DepositWithMonitor(amount);
                        else
                            account.WithdrawWithMonitor(Math.Abs(amount));
                    }
                    catch (InvalidOperationException)
                    {
                        // Недостаточно средств — допустимая ситуация
                    }
                });
                threads.Add(thread);
            }

            foreach (var t in threads)
                t.Start();

            foreach (var t in threads)
                t.Join();

            return account.Balance;
        }

        /// <summary>
        /// Выполняет параллельные переводы между случайными счетами.
        /// Демонстрирует корректную работу без deadlock благодаря
        /// единообразному порядку захвата блокировок (по Id).
        /// </summary>
        public void ProcessConcurrentTransfers(List<BankAccount> accounts, int transferCount)
        {
            if (accounts == null || accounts.Count < 2)
                throw new ArgumentException("Необходимо минимум 2 счёта.", nameof(accounts));

            var random = new Random(42);
            var threads = new List<Thread>();
            int successCount = 0;
            int failCount = 0;

            // Предварительно генерируем данные переводов, чтобы Random не вызывался из нескольких потоков
            var transferData = new List<(int from, int to, decimal amount)>();
            for (int i = 0; i < transferCount; i++)
            {
                int fromIdx = random.Next(accounts.Count);
                int toIdx;
                do
                {
                    toIdx = random.Next(accounts.Count);
                } while (toIdx == fromIdx);

                decimal amount = Math.Round((decimal)(random.NextDouble() * 100 + 1), 2);
                transferData.Add((fromIdx, toIdx, amount));
            }

            foreach (var (fromIdx, toIdx, amount) in transferData)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        accounts[fromIdx].TransferWithLock(accounts[toIdx], amount);
                        Interlocked.Increment(ref successCount);
                    }
                    catch (InvalidOperationException)
                    {
                        // Недостаточно средств — нормальная ситуация
                        Interlocked.Increment(ref failCount);
                    }
                });
                threads.Add(thread);
            }

            foreach (var t in threads)
                t.Start();

            foreach (var t in threads)
                t.Join();

            Console.WriteLine($"  Переводов выполнено успешно: {successCount}");
            Console.WriteLine($"  Переводов отклонено (недостаточно средств): {failCount}");
        }

        /// <summary>
        /// Демонстрация потенциального deadlock при НЕПРАВИЛЬНОМ порядке захвата блокировок.
        /// Используется Monitor.TryEnter с таймаутом, чтобы обнаружить deadlock, а не зависнуть.
        /// </summary>
        public void DemonstrateDeadlockScenario(BankAccount accountA, BankAccount accountB)
        {
            Console.WriteLine("\n--- Демонстрация сценария deadlock ---");
            Console.WriteLine($"Счёт A: {accountA}");
            Console.WriteLine($"Счёт B: {accountB}");

            int deadlockDetected = 0;

            // Поток 1: захватывает A, затем B (НЕПРАВИЛЬНЫЙ порядок, если поток 2 делает наоборот)
            var thread1 = new Thread(() =>
            {
                bool lockA = false;
                bool lockB = false;
                try
                {
                    Monitor.TryEnter(accountA.LockObject, 1000, ref lockA);
                    if (lockA)
                    {
                        Thread.Sleep(50); // увеличиваем вероятность deadlock
                        Monitor.TryEnter(accountB.LockObject, 500, ref lockB);
                        if (!lockB)
                        {
                            Console.WriteLine("  [Поток 1] Обнаружен потенциальный deadlock! Не удалось захватить блокировку B.");
                            Interlocked.Increment(ref deadlockDetected);
                        }
                        else
                        {
                            Console.WriteLine("  [Поток 1] Успешно захватил оба замка (A → B).");
                        }
                    }
                }
                finally
                {
                    if (lockB) Monitor.Exit(accountB.LockObject);
                    if (lockA) Monitor.Exit(accountA.LockObject);
                }
            });

            // Поток 2: захватывает B, затем A (обратный порядок → deadlock)
            var thread2 = new Thread(() =>
            {
                bool lockB = false;
                bool lockA = false;
                try
                {
                    Monitor.TryEnter(accountB.LockObject, 1000, ref lockB);
                    if (lockB)
                    {
                        Thread.Sleep(50);
                        Monitor.TryEnter(accountA.LockObject, 500, ref lockA);
                        if (!lockA)
                        {
                            Console.WriteLine("  [Поток 2] Обнаружен потенциальный deadlock! Не удалось захватить блокировку A.");
                            Interlocked.Increment(ref deadlockDetected);
                        }
                        else
                        {
                            Console.WriteLine("  [Поток 2] Успешно захватил оба замка (B → A).");
                        }
                    }
                }
                finally
                {
                    if (lockA) Monitor.Exit(accountA.LockObject);
                    if (lockB) Monitor.Exit(accountB.LockObject);
                }
            });

            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            if (deadlockDetected > 0)
                Console.WriteLine("  ⚠ Deadlock был обнаружен и предотвращён с помощью Monitor.TryEnter.");
            else
                Console.WriteLine("  ✓ В этом запуске deadlock не возник (но он вероятен при многократных запусках).");

            // Теперь демонстрируем ПРАВИЛЬНЫЙ подход с единообразным порядком
            Console.WriteLine("\n--- Решение: единообразный порядок захвата блокировок ---");
            Console.WriteLine("  Используем TransferWithLock, который захватывает блокировки по возрастанию Id.");

            var threads = new List<Thread>();
            int success = 0;

            for (int i = 0; i < 100; i++)
            {
                int iter = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        if (iter % 2 == 0)
                            accountA.TransferWithLock(accountB, 1m);
                        else
                            accountB.TransferWithLock(accountA, 1m);

                        Interlocked.Increment(ref success);
                    }
                    catch (InvalidOperationException)
                    {
                        // Недостаточно средств
                    }
                });
                threads.Add(t);
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            Console.WriteLine($"  Выполнено {success} переводов без deadlock.");
            Console.WriteLine($"  Счёт A: {accountA}");
            Console.WriteLine($"  Счёт B: {accountB}");
        }
    }
}
