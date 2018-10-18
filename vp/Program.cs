using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace vp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: vp filename");
                return;
            }

            string filename = args[0];
            if (!File.Exists(filename))
            {
                Console.Error.WriteLine("Cannot open \"" + filename + "\"");
                return;
            }

            if (viOpenDefaultRM(out rm) != 0)
                return;

            // viOpen fails if "Enable the Visual Studio hosting process" is checked
            //const string resource = "GPIB8::1::INSTR";
            const string resource = "TCPIP::169.254.56.79::5025::SOCKET";
            int status = viOpen(rm, resource, 0, 0, ref vi);
            if (status != 0)
                return;

            viSetAttribute(vi, VI_ATTR_TMO_VALUE, 3 * 1000);

            // Without the following settings reads timeout on socket connections
            viSetAttribute(vi, VI_ATTR_TERMCHAR, '\n'); // line-feed
            viSetAttribute(vi, VI_ATTR_TERMCHAR_EN, VI_TRUE);

            int count = 0;
            using (StreamReader reader = new StreamReader(filename))
            {
                while (!reader.EndOfStream)
                {
                    string directive = reader.ReadLine();
                    count += 1;
                    if (directive == null)
                        continue;

                    Debug.Write(count.ToString("D7") + ": " + directive);
                    Write(directive);
                    if (directive.Contains("?"))
                    {
                        string response = Read();
                        if (response != null)
                            Debug.WriteLine("  --> " + response.Trim());
                    }
                    else
                    {
                        Debug.WriteLine("");
                    }
                }
            }

            viClose(vi);
            viClose(rm);
        }

        private static void Write(string directive)
        {
            // Terminating all writes with a linefeed character is required for socket connections
            directive += '\n';
            int nBytes = encoding.GetBytes(directive, 0, directive.Length, buffer, 0);
            viWrite(vi, buffer, nBytes, out _);
        }

        private static string Read()
        {
            int totalCount;
            byte[] response;

            int status = viRead(vi, buffer, buffer.Length, out int retCount);

            if (status == VI_ERROR_TMO)
            {
                Console.WriteLine();
                Console.WriteLine("Timeout");
                return null;
            }

            // buffer too small for response
            if (status == VI_SUCCESS_MAX_CNT)
            {
                response = new byte[buffer.Length];
                Array.Copy(buffer, 0, response, 0, retCount);
                totalCount = retCount;
                while (viRead(vi, buffer, buffer.Length, out retCount) == VI_SUCCESS_MAX_CNT)
                {
                    Array.Resize(ref response, totalCount + retCount);
                    Array.Copy(buffer, 0, response, totalCount, retCount);
                    totalCount += retCount;
                }
                Array.Resize(ref response, totalCount + retCount);
                Array.Copy(buffer, 0, response, totalCount, retCount);
                totalCount += retCount;
            }
            else
            {
                response = buffer;
                totalCount = retCount;
            }

            return encoding.GetString(response, 0, totalCount);
        }

        private static uint rm;
        private static uint vi;

        private static readonly Encoding encoding = new ASCIIEncoding();

        private static readonly byte[] buffer = new byte[0x10000];
        private const int VI_SUCCESS_MAX_CNT = 0x3FFF0006;
        private const int VI_ERROR_TMO = -1073807339;   // 0xBFFF0015
        private const uint VI_ATTR_TMO_VALUE = 0x3FFF001AU;
        private const uint VI_ATTR_TERMCHAR = 0x3FFF0018U;
        private const uint VI_ATTR_TERMCHAR_EN = 0x3FFF0038U;
        private const uint VI_TRUE = 1;

        #region VISA32 interface

#pragma warning disable IDE1006 // Naming Styles

        [DllImport("VISA32.dll", CharSet = CharSet.Ansi)]
        private static extern int viOpenDefaultRM(out uint session);

        [DllImport("VISA32.dll", EntryPoint = "viOpen", CharSet = CharSet.Ansi)]
        private static extern int viOpen(uint session, string resource, uint mode, uint timeout, ref uint vi);

        [DllImport("VISA32.dll", CharSet = CharSet.Ansi)]
        private static extern int viSetAttribute(uint vi, uint attribute, uint value);

        [DllImport("VISA32.dll", CharSet = CharSet.Ansi)]
        private static extern int viWrite(uint vi, byte[] buf, int count, out int retCount);

        [DllImport("VISA32.dll", CharSet = CharSet.Ansi)]
        private static extern int viRead(uint vi, byte[] buf, int count, out int retCount);

        [DllImport("VISA32.dll", CharSet = CharSet.Ansi)]
        private static extern int viClose(uint viobj);

#pragma warning restore IDE1006 // Naming Styles

        #endregion
    }
}
