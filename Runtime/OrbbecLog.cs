using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OrbbecUnity
{
    static class OrbbecLog
    {
        static readonly object Gate = new object();
        static StreamWriter writer;

        public static void Info(string message)
        {
            Write(message, false);
        }

        public static void Error(string message)
        {
            Write(message, true);
        }

        static void Write(string message, bool error)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff ") + message;
            try
            {
                lock (Gate)
                {
                    if (writer == null)
                    {
                        string directory = DirectoryOf(Application.consoleLogPath);
                        if (string.IsNullOrEmpty(directory))
                        {
                            directory = Application.persistentDataPath;
                        }

                        string path = Path.Combine(directory, "Orbbec.log");
                        writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
                        {
                            AutoFlush = true,
                        };
                        writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ") + "日志文件 " + path);
                    }

                    writer.WriteLine(line);
                }
            }
            catch (Exception)
            {
            }

            if (error)
            {
                Debug.LogError("[Orbbec] " + message);
            }
            else
            {
                Debug.Log("[Orbbec] " + message);
            }
        }

        static string DirectoryOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
