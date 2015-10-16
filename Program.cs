using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ActivityProfiler
{
    /// <summary>
    /// Name: User Activity Profiler
    /// Author: Opata Chibueze
    /// Description: Profiles user activity for your C# Winform Applications
    /// Currently scans for click related events
    /// In future, we can also watch for tab switch and key press related events
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string solutionDir = String.Empty;
            if (args.Length == 0)
            {
                //use project solution folder
                solutionDir = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            }
            else
            {
                solutionDir = args[0];
            }
            string namspace = null;
            foreach (string f in Directory.EnumerateFiles(solutionDir, "*.cs", SearchOption.AllDirectories))
            {
                bool edited = false;
                //skip generated files
                if (f.Contains("obj\\") || f.Contains("Properties\\"))
                {
                    continue;
                }
                string originalText = File.ReadAllText(f);
                StringBuilder finalText = new StringBuilder(originalText);
                var col = Regex.Matches(originalText, "^.*\\s(\\S*?_+)Click(.*?)\\(", RegexOptions.Multiline);
                int insertLength = 0;
                for (int i = 0; i < col.Count; i++)
                {
                    if (col[i].Groups.Count < 3)
                    {
                        //should be impossible but JIC
                        continue;
                    }
                    string methodName = col[i].Groups[1].Value + "Click" + col[i].Groups[2].Value;
                    if (methodName.Contains("=") || methodName.Contains(" "))
                    {
                        //invalid method, skip
                        continue;
                    }
                    int classIndex = originalText.LastIndexOf("class", col[i].Index, StringComparison.Ordinal) + 1;
                    //we can use { or space to find classIndex, maybe do all this with regex in future
                    int classIndex1 = originalText.IndexOf(' ', classIndex + 6);
                    int classBrace = originalText.IndexOf('{', classIndex + 6);
                    //determine better index to use
                    int finalIndex = classIndex1 > classBrace ? classBrace : classIndex1;
                    string className = originalText.Substring(classIndex + 5, finalIndex - classIndex - 5).TrimEnd('\r', '\n', ' ', '\t');
                    int braceIndex = originalText.IndexOf('{', col[i].Index);
                    int[] prepend = getInsertPrepend(originalText, braceIndex + 1);
                    int closeIndex = originalText.IndexOf('{', braceIndex + 1);
                    string insert = string.Format("{0}{3}ActivityProfiler.Clicks.Log(\"{1}.{2}\");", Environment.NewLine, className, methodName,
                        new string(' ', prepend[1] - prepend[0] - prepend[2]));
                    //account for inserted text in next insert
                    finalText.Insert(prepend[0] + insertLength, insert);
                    insertLength += insert.Length;
                    edited = true;

                    //determine if we're still in same namespace
                    int nameIndex = originalText.LastIndexOf("namespace", classIndex, StringComparison.Ordinal) + 1;
                    int nameIndex1 = originalText.IndexOf('{', nameIndex + 10);
                    string nspace = originalText.Substring(nameIndex + 9, nameIndex1 - nameIndex - 9).TrimEnd('\r', '\n', ' ', '\t');

                    if (nspace != namspace)
                    {
                        finalText.Insert(originalText.Length + insertLength, @"

namespace ActivityProfiler
{
    public class Clicks
    {
        private static System.Collections.Generic.Dictionary<string, int> clicksLog = new System.Collections.Generic.Dictionary<string, int>();
        private static bool block = false;
        public static void Log(string method)
        {
            // thread A
            lock (clicksLog)
            {
                while (block)
                    System.Threading.Monitor.Wait(clicksLog);
                block = true;
                if (!clicksLog.ContainsKey(method))
                {
                    clicksLog.Add(method, 0);
                }
                clicksLog[method]++;
                string dir = System.Environment.ExpandEnvironmentVariables(""%windir%"") + ""\\Logs"";
                if (!System.IO.Directory.Exists(dir))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    catch
                    {
                        dir = System.Environment.CurrentDirectory;
                    }
                }
                try
                {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(dir + ""\\activity_profile.log"", false))
                    {
                            foreach (var kvp in clicksLog)
                            {
                                sw.WriteLine(kvp.Key + "": "" + kvp.Value);
                                clicksLog.Clear();
                            }
                        }
                    }
                finally
                {
                    block = false;
                    System.Threading.Monitor.Pulse(clicksLog);
                }
            }
        }
    }
}");
                        namspace = nspace;
                    }
                }
                if (edited)
                {
                    if (!File.Exists(f + ".backup"))
                    {
                        File.Move(f, f + ".backup");
                        File.WriteAllText(f, finalText.ToString());
                    }
                }
            }
        }

        //quick/dirty helper method to help get amount of space to give before insertion
        public static int[] getInsertPrepend(string s, int startIndex)
        {
            int[] result = { -1, -1, 0 };
            for (int i = startIndex; i < s.Length; i++)
            {
                if (Char.IsWhiteSpace(s[i]))
                {
                    for (int j = i; j < s.Length; j++)
                    {
                        if (!Char.IsWhiteSpace(s[j]))
                        {
                            result[0] = i;
                            result[1] = j;
                            //correct strange quirk with indexing on empty methods
                            result[2] = Char.IsLetter(s[j]) ? 2 : 0;
                            return result;
                        }
                    }
                }
            }
            return result;
        }        
    }
}