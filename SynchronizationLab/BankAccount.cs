using System;
using System.Threading;

namespace SynchronizationLab
{
    /// <summary>
    /// Класс банковского счёта с различными стратегиями синхронизации.
    /// Демонстрирует проблему гонок данных и способы её решения.
    /// </summary>
    public class BankAccount
    {
        // Уникальный идентификатор счёта (используется для упорядочивания блокировок)
        private static int _nextId = 0;
        public int Id { get; }

        // Баланс счёта — decimal для точных финансовых вычислений
        private decimal _balance;
        public decimal Balance
        {
            get => _balance;
            private set => _balance = value;
        }

        public string Owner { get; }

        // Приватный объект-замок для синхронизации
        private readonly object _lock = new object();

        /// <summary>
        /// Предоставляет доступ к объекту блокировки для внешней синхронизации
        /// (используется в TransferWithLock/TransferWithMonitor для захвата двух замков).
        /// </summary>
        public object LockObject => _lock;

        public BankAccount(string owner, decimal initialBalance)
        {
            Id = Interlocked.Increment(ref _nextId);
            Owner = owner;

            if (initialBalance < 0)
                throw new ArgumentException("Начальный баланс не может быть отрицательным.", nameof(initialBalance));

            _balance = initialBalance;
        }

        // =====================================================================
        // 1. Базовая реализация БЕЗ синхронизации (для демонстрации гонок)
        // =====================================================================

        /// <summary>Пополнение без синхронизации.</summary>
        public void Deposit(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма пополнения должна быть положительной.", nameof(amount));

            // Искусственная задержка усиливает вероятность гонки
            decimal temp = _balance;
            Thread.SpinWait(100);
            _balance = temp + amount;
        }

        /// <summary>Снятие без синхронизации.</summary>
        public void Withdraw(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма снятия должна быть положительной.", nameof(amount));

            decimal temp = _balance;
            Thread.SpinWait(100);
            // Допускаем отрицательный баланс, чтобы не маскировать гонку
            _balance = temp - amount;
        }

        /// <summary>Перевод без синхронизации.</summary>
        public void Transfer(BankAccount target, decimal amount)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (amount <= 0) throw new ArgumentException("Сумма перевода должна быть положительной.", nameof(amount));

            Withdraw(amount);
            target.Deposit(amount);
        }

        // =====================================================================
        // 2. Реализация с использованием lock
        // =====================================================================

        /// <summary>Пополнение с использованием lock.</summary>
        public void DepositWithLock(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма пополнения должна быть положительной.", nameof(amount));

            lock (_lock)
            {
                _balance += amount;
            }
        }

        /// <summary>Снятие с использованием lock.</summary>
        public void WithdrawWithLock(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма снятия должна быть положительной.", nameof(amount));

            lock (_lock)
            {
                if (_balance < amount)
                    throw new InvalidOperationException(
                        $"Недостаточно средств. Баланс: {_balance}, запрошено: {amount}");

                _balance -= amount;
            }
        }

        /// <summary>
        /// Перевод с использованием lock.
        /// Блокировки захватываются в порядке возрастания Id для предотвращения deadlock.
        /// </summary>
        public void TransferWithLock(BankAccount target, decimal amount)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (amount <= 0) throw new ArgumentException("Сумма перевода должна быть положительной.", nameof(amount));
            if (target == this) throw new ArgumentException("Нельзя перевести средства на тот же счёт.", nameof(target));

            // Единообразный порядок захвата замков по Id предотвращает deadlock
            object firstLock = Id < target.Id ? _lock : target._lock;
            object secondLock = Id < target.Id ? target._lock : _lock;

            lock (firstLock)
            {
                lock (secondLock)
                {
                    if (_balance < amount)
                        throw new InvalidOperationException(
                            $"Недостаточно средств для перевода. Баланс: {_balance}, запрошено: {amount}");

                    _balance -= amount;
                    target._balance += amount;
                }
            }
        }

        // =====================================================================
        // 3. Реализация с использованием Monitor
        // =====================================================================

        /// <summary>Пополнение с использованием Monitor.</summary>
        public void DepositWithMonitor(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма пополнения должна быть положительной.", nameof(amount));

            bool lockTaken = false;
            try
            {
                Monitor.Enter(_lock, ref lockTaken);
                _balance += amount;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_lock);
            }
        }

        /// <summary>Снятие с использованием Monitor.</summary>
        public void WithdrawWithMonitor(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма снятия должна быть положительной.", nameof(amount));

            bool lockTaken = false;
            try
            {
                Monitor.Enter(_lock, ref lockTaken);

                if (_balance < amount)
                    throw new InvalidOperationException(
                        $"Недостаточно средств. Баланс: {_balance}, запрошено: {amount}");

                _balance -= amount;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// Перевод с использованием Monitor.
        /// Блокировки захватываются в порядке возрастания Id для предотвращения deadlock.
        /// </summary>
        public void TransferWithMonitor(BankAccount target, decimal amount)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (amount <= 0) throw new ArgumentException("Сумма перевода должна быть положительной.", nameof(amount));
            if (target == this) throw new ArgumentException("Нельзя перевести средства на тот же счёт.", nameof(target));

            // Определяем порядок блокировки по Id
            object firstLock = Id < target.Id ? _lock : target._lock;
            object secondLock = Id < target.Id ? target._lock : _lock;

            bool firstLockTaken = false;
            bool secondLockTaken = false;
            try
            {
                Monitor.Enter(firstLock, ref firstLockTaken);
                Monitor.Enter(secondLock, ref secondLockTaken);

                if (_balance < amount)
                    throw new InvalidOperationException(
                        $"Недостаточно средств для перевода. Баланс: {_balance}, запрошено: {amount}");

                _balance -= amount;
                target._balance += amount;
            }
            finally
            {
                if (secondLockTaken)
                    Monitor.Exit(secondLock);
                if (firstLockTaken)
                    Monitor.Exit(firstLock);
            }
        }

        // =====================================================================
        // 4. Реализация с таймаутами (Monitor.TryEnter)
        // =====================================================================

        /// <summary>Пополнение с таймаутом. Возвращает true при успехе.</summary>
        public bool DepositWithTimeout(decimal amount, int timeoutMs)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма пополнения должна быть положительной.", nameof(amount));

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_lock, timeoutMs, ref lockTaken);

                if (!lockTaken)
                {
                    // Не удалось захватить блокировку за отведённое время
                    Console.WriteLine($"[Таймаут] Не удалось захватить блокировку для депозита {amount} за {timeoutMs} мс.");
                    return false;
                }

                _balance += amount;
                return true;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_lock);
            }
        }

        /// <summary>Снятие с таймаутом. Возвращает true при успехе.</summary>
        public bool WithdrawWithTimeout(decimal amount, int timeoutMs)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма снятия должна быть положительной.", nameof(amount));

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_lock, timeoutMs, ref lockTaken);

                if (!lockTaken)
                {
                    Console.WriteLine($"[Таймаут] Не удалось захватить блокировку для снятия {amount} за {timeoutMs} мс.");
                    return false;
                }

                if (_balance < amount)
                {
                    Console.WriteLine($"[Отказ] Недостаточно средств. Баланс: {_balance}, запрошено: {amount}");
                    return false;
                }

                _balance -= amount;
                return true;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_lock);
            }
        }

        /// <summary>Сброс баланса к указанному значению (для повторного тестирования).</summary>
        public void ResetBalance(decimal balance)
        {
            _balance = balance;
        }

        public override string ToString() =>
            $"Счёт #{Id} ({Owner}): {_balance:C}";
    }
}
