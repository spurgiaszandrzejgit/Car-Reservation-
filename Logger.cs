using System;
using System.IO;

namespace vaurioajoneuvo_finder
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string LogFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoreserve_log.txt");

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));

                // Date 
                if (File.Exists(LogFilePath))
                {
                    var lastWrite = File.GetLastWriteTime(LogFilePath).Date;
                    if (lastWrite < DateTime.Now.Date)
                    {
                        // cleanig log file
                        File.WriteAllText(LogFilePath, "");
                        Console.WriteLine("[LOGGER] Old log file (new dzień).");
                    }
                }

                File.AppendAllText(LogFilePath,
                    $"{Environment.NewLine}==== START ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===={Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER INIT ERROR] {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}";
            Write(line);
        }

        public static void LogEx(string context, Exception ex)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {context} [Błąd: {ex.GetType().Name}: {ex.Message}]";
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
                Console.WriteLine(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER WRITE ERROR] {ex.Message}");
            }
        }
    }
}
