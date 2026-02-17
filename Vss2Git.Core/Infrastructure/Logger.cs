using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Writes log messages to an optional stream.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class Logger : IDisposable
    {
        public static readonly Logger Null = new Logger((Stream)null);

        private const string sectionSeparator = "------------------------------------------------------------";

        private readonly Stream baseStream;
        private readonly Encoding encoding;
        private readonly IFormatProvider formatProvider;

        public Logger(string filename)
            : this(new BufferedStream(
                new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read),
                bufferSize: 65536)) // 64KB buffer for much better performance
        {
        }

        public Logger(Stream baseStream)
            : this(baseStream, Encoding.Default, CultureInfo.InvariantCulture)
        {
        }

        public Logger(Stream baseStream, Encoding encoding, IFormatProvider formatProvider)
        {
            this.baseStream = baseStream;
            this.encoding = encoding;
            this.formatProvider = formatProvider;
        }

        public void Dispose()
        {
            if (baseStream != null)
            {
                baseStream.Dispose();
            }
        }

        public void Write(bool value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(char value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(char[] buffer)
        {
            if (baseStream != null && buffer != null)
            {
                Write(buffer, 0, buffer.Length);
            }
        }

        public void Write(decimal value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(double value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(float value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(int value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(long value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(object value)
        {
            if (baseStream != null && value != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(string value)
        {
            if (baseStream != null && value != null)
            {
                WriteInternal(value);
            }
        }

        public void Write(uint value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(ulong value)
        {
            if (baseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(string format, params object[] arg)
        {
            if (baseStream != null && arg != null)
            {
                Write(string.Format(formatProvider, format, arg));
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            if (baseStream != null && buffer != null)
            {
                WriteInternal(buffer, index, count);
            }
        }

        public void WriteLine()
        {
            Write(Environment.NewLine);
        }

        public void WriteLine(bool value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(char value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(char[] buffer)
        {
            if (baseStream != null && buffer != null)
            {
                WriteInternal(buffer, 0, buffer.Length);
                WriteLine();
            }
        }

        public void WriteLine(decimal value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(double value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(float value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(int value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(long value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(object value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(string value)
        {
            if (baseStream != null)
            {
                WriteInternal(value);
                WriteLine();
            }
        }

        public void WriteLine(uint value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(ulong value)
        {
            if (baseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }
        }

        public void WriteLine(string format, params object[] arg)
        {
            if (baseStream != null && arg != null)
            {
                WriteInternal(string.Format(formatProvider, format, arg));
                WriteLine();
            }
        }

        public void WriteLine(char[] buffer, int index, int count)
        {
            if (baseStream != null && buffer != null)
            {
                WriteInternal(buffer, index, count);
                WriteLine();
            }
        }

        public void WriteSectionSeparator()
        {
            WriteLine(sectionSeparator);
            Flush();
        }

        public void Flush()
        {
            if (baseStream != null)
            {
                baseStream.Flush();
            }
        }

        /// <summary>
        /// Rotates an existing log file using numbered suffixes (.1, .2, .3, etc.).
        /// Call before creating a new Logger to preserve previous session logs.
        /// </summary>
        public static void RotateLogFile(string filename)
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
                return;

            var dir = Path.GetDirectoryName(filename) ?? ".";
            var name = Path.GetFileNameWithoutExtension(filename);
            var ext = Path.GetExtension(filename);

            // Find the highest existing numbered log
            int max = 0;
            while (File.Exists(Path.Combine(dir, $"{name}.{max + 1}{ext}")))
                max++;

            // Shift numbered files up: .3 → .4, .2 → .3, .1 → .2
            for (int i = max; i >= 1; i--)
            {
                File.Move(
                    Path.Combine(dir, $"{name}.{i}{ext}"),
                    Path.Combine(dir, $"{name}.{i + 1}{ext}"));
            }

            // Current log becomes .1
            File.Move(filename, Path.Combine(dir, $"{name}.1{ext}"));
        }

        private void WriteInternal(string value)
        {
            var bytes = encoding.GetBytes(value);
            baseStream.Write(bytes, 0, bytes.Length);
        }

        private void WriteInternal(char[] buffer, int index, int count)
        {
            var bytes = encoding.GetBytes(buffer, index, count);
            baseStream.Write(bytes, 0, bytes.Length);
        }
    }
}
