using System;

namespace iTunesMatchTagger
{
    /// <summary>
    /// Logging
    /// </summary>
    class Logging
    {
        public delegate void WriteLogEventHandler(object sender, LoggingEventArgs args);

        public enum Severity
        {
            Debug,
            Information,
            Warning,
            Error
        }

        public class LoggingEventArgs : EventArgs
        {
            public string Message;
            public Severity Severity;

            public LoggingEventArgs(string message, Severity severity)
            {
                Message = message;
                Severity = severity;
            }
        }

        public static event WriteLogEventHandler OnWriteLog;

        public static void WriteLog(object sender, string message, Severity severity)
        {
            if (OnWriteLog != null) OnWriteLog(sender, new LoggingEventArgs(message, severity));
        }

        public static void WriteLog(object sender, LoggingEventArgs args)
        {
            if (OnWriteLog != null) OnWriteLog(sender, args);
        }
    }
}