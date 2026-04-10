using System;
using System.IO;
using UnityEngine;

namespace Core.Common
{
    public static class HitTraceLogger
    {
        private static readonly object Sync = new object();
        private static bool _initialized;
        private static string _logPath;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }

        public static void Log(string message)
        {
            EnsureInitialized();

            string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
            Debug.LogWarning(line);
            AppendLineSafely(line);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RuntimeBoot()
        {
            Log($"[HitTrace][BOOT][Runtime] unityLogEnabled={Debug.unityLogger.logEnabled} persistentDataPath={Application.persistentDataPath}");
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _logPath = Path.Combine(Application.persistentDataPath, "HitTrace.log");
                _initialized = true;
                AppendLineSafely($"{DateTime.Now:HH:mm:ss.fff} [HitTrace][BOOT] LoggerInitialized path={_logPath}");
            }
        }

        private static void AppendLineSafely(string line)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // 디버그 파일 기록 실패는 런타임 흐름을 막지 않는다.
            }
        }
    }
}
