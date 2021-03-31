using System;
using System.Collections.Generic;

namespace ArgParser
{

    class Parser
    {
        public Dictionary<string, string> argValues = new Dictionary<string, string>();
        Dictionary<string, List<string>> argOptions = new Dictionary<string, List<string>>();
        string helpText;

        void ShowHelpText()
        {
            Console.WriteLine(helpText);
            Console.WriteLine("\nArguments:");
            foreach (var arg in argOptions)
            {
                Console.WriteLine("\t" + arg.Value[0] + " / " + arg.Value[1] + "\t" + arg.Value[2] + "\n");
            }
        }

        public void AddHelpText(string helpText)
        {
            this.helpText = helpText;
        }

        public void AddArgument(string argKey, string argShort, string argLong, string argHelpText)
        {
            this.argOptions[argKey] = new List<String> { argShort, argLong, argHelpText };
        }

        string FindKey(string param)
        {
            foreach (var argOption in argOptions)
            {
                if (argOption.Value.Contains(param))
                {
                    return argOption.Key;
                }
            }
            return null;
        }

        public void ParseArguments(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelpText();
                System.Environment.Exit(0);
            }
            else
            {
                Dictionary<string, string> argParams = new Dictionary<string, string>();
                foreach (string arg in args)
                {
                    string[] argSplit = new string[2];
                    if (arg.StartsWith("--"))
                    {
                        argSplit = arg.Split('=');
                        if (argSplit.Length > 1)
                        {
                            argParams[argSplit[0]] = argSplit[1];
                        }
                        else
                        {
                            argParams[argSplit[0]] = "";
                        }
                    }
                    else if (arg.StartsWith("-"))
                    {
                        argSplit = arg.Split(' ');
                        if (argSplit.Length > 1)
                        {
                            argParams[argSplit[0]] = argSplit[1];
                        }
                        else if (argSplit[0].Length > 2)
                        {
                            argParams[argSplit[0].Substring(0, 2)] = argSplit[0].Substring(2);
                        }
                        else
                        {
                            argParams[argSplit[0]] = "";
                        }
                    }
                }
                foreach (var argParam in argParams)
                {
                    string argKey = FindKey(argParam.Key);
                    if (argKey != null)
                    {
                        argValues[argKey] = argParam.Value;
                    }
                }
            }
        }
    }
    
}
