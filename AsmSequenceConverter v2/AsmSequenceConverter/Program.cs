using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsmSequenceConverter
{
    class Program
    {
        public static ConcurrentDictionary<string, string> UniqueAsmCommands = new ConcurrentDictionary<string, string>();

        public static string trainDir;
        public static string testDir;
        public static string asmCommandPath = Directory.GetCurrentDirectory() + "\\AsmCommands.txt";

        static void Main(string[] args)
        {

            Console.WriteLine("Train Directory:");
            trainDir = Console.ReadLine();

            Console.WriteLine("Test Directory:");
            testDir = Console.ReadLine();

            Console.WriteLine("Overwrite Old Files? (Y / N):");
            bool overwrite = Console.ReadLine() == "Y" ? true : false;

            Console.WriteLine("Max Threads:");
            int maxThreads = int.Parse(Console.ReadLine());


            //Get rid of any files from previous runs.
            if (overwrite)
            {
                //Get rid of any files from previous runs.
                DeleteOldFiles(trainDir);
                DeleteOldFiles(testDir);
            }

            //Load a valid list of assembly commands from AsmCommands.txt
            LoadAsmCommands(asmCommandPath);

            //Map each unique command to a sequence word
            MapAsmCommandsToSequenceWords();

            //******************************************************************
            //Make a list of the asm files to process
            //******************************************************************

            //Get all the file paths from train and test...
            string[] trainFiles = Directory.GetFiles(trainDir, "*.asm");
            string[] testFiles = Directory.GetFiles(testDir, "*.asm");

            //Combine the two lists of files...
            List<string> filesToProcess = new List<string>(trainFiles);
            filesToProcess.AddRange(testFiles);

            //Convert each file to a gene sequence
            Stopwatch sw = new Stopwatch();
            sw.Start();
            ConvertAsmFilesToSequences(filesToProcess, overwrite, maxThreads);
            sw.Stop();

            //Write out final stats
            Console.WriteLine();
            Console.WriteLine("Processing Time: " + sw.Elapsed);
            Console.WriteLine("Unique Assembly Commands: " + UniqueAsmCommands.Count);
            Console.WriteLine("Sequence Word Length: " + UniqueAsmCommands["jmp"].Length);
            Console.WriteLine("Unique Sequence Word Space: " + Math.Pow(4,UniqueAsmCommands["jmp"].Length));
            Console.ReadLine();
        }

        public static void DeleteOldFiles(string inputDir)
        {
            var dir = new DirectoryInfo(inputDir);

            //Delete any only asmcmd files from a previous run
            foreach (var file in dir.EnumerateFiles("*.asmcmd"))
            {
                file.Delete();
            }
            //Delete any only asmseq files from a previous run
            foreach (var file in dir.EnumerateFiles("*.asmseq"))
            {
                file.Delete();
            }
        }

        public static void ConvertAsmFilesToSequences(List<string> filesToProcess, bool overwrite, int maxThreads)
        {
            int fcount = 0;

            Parallel.ForEach(filesToProcess, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, path =>
            {
                //StringBuilder asmCommandFile = new StringBuilder();

                string fileExt = Path.GetExtension(path);
                if (fileExt == ".asm")
                {
                    //Create a new file with sequence data for each input file
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string fileDir = Path.GetDirectoryName(path);
                    bool seqFileExists = File.Exists(fileDir + "\\" + fileName + ".asmseq");

                    if (overwrite || seqFileExists == false)
                    {

                        StringBuilder sequenceData = new StringBuilder();

                        foreach (var line in File.ReadAllLines(path))
                        {
                            //crop line numbers
                            string subLine = line.Length >= 14 ? line.Substring(14) : "";
                            int firstTab = subLine.IndexOf("\t\t");
                            //avoid comments
                            if (firstTab >= 0)
                            {
                                string hexCode = subLine.Substring(0, firstTab);

                                // if there is hex, then find the assembly command
                                if (hexCode != "")
                                {   //asm command is after the first set of multiple tabs
                                    int asmIndex = subLine.IndexOf(' ', firstTab);
                                    if (asmIndex >= 0)
                                    {
                                        string asmCode = subLine.Substring(asmIndex).Trim();
                                        int asmCodeSpacePos = asmCode.IndexOf(' ');
                                        string asmCommand = asmCodeSpacePos >= 0 ? asmCode.Substring(0, asmCodeSpacePos) : asmCode;

                                        //We only use asmCommand values which were determined in advance to  
                                        //be valid assembly commands on the AsmCommands.txt list and already mapped to sequence words
                                        string asmCmdSequenceWord = "";
                                        if (UniqueAsmCommands.TryGetValue(asmCommand, out asmCmdSequenceWord))
                                            sequenceData.Append(asmCmdSequenceWord);
                                    }
                                }
                            }
                        }

                        //Create a new file with sequence data for each input file
                        File.WriteAllText(fileDir + "\\" + fileName + ".asmseq", sequenceData.ToString());
                        sequenceData.Clear();

                        //Keep track of the files we have processed
                        Interlocked.Increment(ref fcount);
                        Console.WriteLine("Files Processed: " + fcount);
                    }
                    else
                    {
                        Interlocked.Increment(ref fcount);
                        Console.WriteLine("Seq File Exists: " + fcount);
                    }
                }
            });
        }
 
        public static void LoadAsmCommands(string asmCmdPath)
        {
            foreach (var asmCommand in File.ReadLines(asmCmdPath))
                UniqueAsmCommands.TryAdd(asmCommand, "");
        }

        public static void MapAsmCommandsToSequenceWords()
        {
            //******************************************************************
            //Find out what length sequence words we need to create base on # of unique commands
            //******************************************************************

            int uniquecommands = UniqueAsmCommands.Count;
            int uniqueSeqWords = 4;
            int seqWordLength = 1;

            while (uniqueSeqWords < uniquecommands)
            {
                uniqueSeqWords *= 4;
                seqWordLength++;
            }

            //******************************************************************
            //Create a unique sequence word for each unique assembly command and create a mapping file (for audit)
            //******************************************************************

            //get a sorted list of the unique asm commands
            string[] asmCmdSorted = UniqueAsmCommands.Keys.ToArray();
            Array.Sort(asmCmdSorted);

            //Create a unique sequence word for each asm command
            StringBuilder asmCmdList = new StringBuilder();
            string wordPad = new String('A', seqWordLength);
            int wordNumber = 0;
            foreach (var asmCommand in asmCmdSorted)
            {
                string wordNumberBaseFour = DecimalToArbitraryBase(wordNumber, 4);
                string padding = wordPad.Substring(0, wordPad.Length - wordNumberBaseFour.Length);
                string b4Sequence = WordNumberToSeq(wordNumberBaseFour);
                string nextWord = padding + b4Sequence;

                UniqueAsmCommands[asmCommand] = nextWord;
                asmCmdList.AppendLine(asmCommand + "," + nextWord);
                wordNumber++;
            }

            //Create a file with the asm command to sequence word mapping  
            File.WriteAllText(trainDir + "\\" + "AsmCommandToSeqWordMap.csv", asmCmdList.ToString());
        }

        /// <summary>
        /// http://stackoverflow.com/questions/923771/quickest-way-to-convert-a-base-10-number-to-any-base-in-net
        /// Converts the given decimal number to the numeral system with the
        /// specified radix (in the range [2, 36]).
        /// </summary>
        /// <param name="decimalNumber">The number to convert.</param>
        /// <param name="radix">The radix of the destination numeral system (in the range [2, 36]).</param>
        /// <returns></returns>
        public static string DecimalToArbitraryBase(long decimalNumber, int radix)
        {
            const int BitsInLong = 64;
            const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            if (radix < 2 || radix > Digits.Length)
                throw new ArgumentException("The radix must be >= 2 and <= " + Digits.Length.ToString());

            if (decimalNumber == 0)
                return "0";

            int index = BitsInLong - 1;
            long currentNumber = Math.Abs(decimalNumber);
            char[] charArray = new char[BitsInLong];

            while (currentNumber != 0)
            {
                int remainder = (int)(currentNumber % radix);
                charArray[index--] = Digits[remainder];
                currentNumber = currentNumber / radix;
            }

            string result = new String(charArray, index + 1, BitsInLong - index - 1);
            if (decimalNumber < 0)
            {
                result = "-" + result;
            }

            return result;
        }

        public static string WordNumberToSeq(string Base4Word)
        {
            StringBuilder seq = new StringBuilder();
            foreach (var baseValue in Base4Word)
            {
                switch (baseValue)
                {
                    case '0':
                        seq.Append('A');
                        break;
                    case '1':
                        seq.Append('C');
                        break;
                    case '2':
                        seq.Append('G');
                        break;
                    case '3':
                        seq.Append('T');
                        break;
                }
            }
            return seq.ToString();
        }
    }
}
