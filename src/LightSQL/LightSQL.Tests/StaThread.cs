namespace LightSQL.Tests
{
    /// <summary>
    /// Runs test code on a new STA thread, which WPF requires for loading XAML and
    /// creating controls.
    /// </summary>
    public static class StaThread
    {
        /// <summary>
        /// Runs a function on a new STA thread and returns its result, rethrowing any
        /// exception it throws.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="function">The function to run.</param>
        /// <returns>The function's result.</returns>
        public static T Run<T>(Func<T> function)
        {
            T result = default!;
            Exception? failure = null;

            Thread thread = new(() =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                throw new InvalidOperationException($"STA work failed: {failure.Message}", failure);
            }

            return result;
        }

        /// <summary>
        /// Runs an action on a new STA thread, rethrowing any exception it throws.
        /// </summary>
        /// <param name="action">The action to run.</param>
        public static void Run(Action action)
        {
            Run(() =>
            {
                action();

                return true;
            });
        }
    }
}
