namespace Exceptions
{
     public class ExceptionManager<TException>
        where TException : Exception, new()
    {
        public void Log<TLogger>(TException ex)
            where TLogger : IExceptionLogger, new()
        {
            var logger = new TLogger();
            logger.Log(ex);
        }

        public TException CreateNew(string message)
        {
            return (TException)Activator.CreateInstance(typeof(TException), message)!
                ?? throw new InvalidOperationException("Could not create exception instance.");
        }

        public void LogAny<TLogger>(Exception ex) // kad pasint bet koki exception
            where TLogger : IExceptionLogger, new()
        {
            var logger = new TLogger();
            logger.Log(ex);
        }
    }
}