using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmFileToSequence
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Input Directory:");
            string inputDir = Console.ReadLine();

            //@"D:\StrandKaggleMalware_Journal\KaggleSampleShort";


            Dictionary<string, string> uniqueAsmCommands = new Dictionary<string, string>();

            //******************************************************************
            //Extract all assembly commands and find a list of unqiue commands 
            //******************************************************************

            int fcount = 0;
            string[] asmfiles = Directory.GetFiles(inputDir, "*.asm");
            StringBuilder asmCommandFile = new StringBuilder();
            foreach (var path in asmfiles)
            {
                string fileExt = Path.GetExtension(path);
                if (fileExt != ".asm") continue;

                //Extract valid assembly commands from each line in the .asm files
                foreach (var asmCommand in AsmToSequence(path))
                {   //track unique commands in all files
                    if (!uniqueAsmCommands.ContainsKey(asmCommand))
                        uniqueAsmCommands.Add(asmCommand, "");
                    //add each command to the .asmcmd file 
                    asmCommandFile.AppendLine(asmCommand);
                }

                //Create a new file with only the assembly commands for each input file
                string fileName = Path.GetFileNameWithoutExtension(path);
                File.WriteAllText(inputDir + "\\" + fileName + ".asmcmd", asmCommandFile.ToString());
                asmCommandFile.Clear();

                //Keep track of the files we have processed
                fcount++;
                Console.WriteLine("Files Processed: " + fcount);
            }

            Console.WriteLine("");

            //******************************************************************
            //Find out what length sequence words we need to create base on # of unique commands
            //******************************************************************

            int uniquecommands = uniqueAsmCommands.Count;
            int uniqueSeqWords = 4;
            int seqWordLength = 1;

            while (uniqueSeqWords < uniquecommands)
            {
                uniqueSeqWords *= 4;
                seqWordLength++;
            }

            Console.WriteLine("Unique Assembly Commands: " + uniquecommands);
            Console.WriteLine("Sequence Word Length: " + seqWordLength);
            Console.WriteLine("Unique Sequence Word Space: " + uniqueSeqWords);

            //******************************************************************
            //Create a unique sequence word for each unique assembly command and create a mapping file (for audit)
            //******************************************************************

            //get a sorted list of the unique asm commands
            string[] asmCmdSorted = uniqueAsmCommands.Keys.ToArray();
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

                uniqueAsmCommands[asmCommand] = nextWord;
                asmCmdList.AppendLine(asmCommand + "," + nextWord);
                wordNumber++;
            }
            //Create a file with the asm command to sequence word mapping  
            File.WriteAllText(inputDir + "\\" + "asmCommandToSeqWordMap.csv", asmCmdList.ToString());

            //******************************************************************
            //Now that we know how many unique commands we have, we create sequence files by concatenating seq words together
            //******************************************************************

            fcount = 0;
            string[] asmCmdfiles = Directory.GetFiles(inputDir, "*.asmcmd");
            StringBuilder sequenceData = new StringBuilder();

            foreach (var path in asmCmdfiles)
            {

                foreach (var asmCommand in File.ReadLines(path))
                {   //Look up the correct sequence word for each assembly command
                    string sequenceWord = uniqueAsmCommands[asmCommand];
                    //Append it to the sequence
                    sequenceData.Append(sequenceWord);
                }
                //Create a new file with sequence data for each input file
                string fileName = Path.GetFileNameWithoutExtension(path);
                File.WriteAllText(inputDir + "\\" + fileName + ".asmseq", sequenceData.ToString());
                sequenceData.Clear();

                fcount++;
            }
            Console.WriteLine("New Sequence Files Created: " + fcount);


            Console.ReadLine();

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

        /// <summary>
        /// We may want to filter by a list of valid ASM commands here.  However, I have not found a comprehensive list yet...
        /// http://www.felixcloutier.com/x86/
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static IEnumerable<string> AsmToSequence(string filePath)
        {
            Dictionary<string, bool> PureCodeSegmentTypes = new Dictionary<string, bool>();

            //if (Path.GetFileNameWithoutExtension(filePath) == "0Hrfce4X5YGESJPjl9uL")
            //    Console.WriteLine("found!");

            foreach (var line in File.ReadLines(filePath))
            {
                string segmentType = line.Substring(0, line.IndexOf(":"));

                //Check for segment type line
                if (line.Contains("Segment") && line.Contains("type"))
                {   //see if this is a known segment type
                    bool pureCodeSegment = false;
                    if (PureCodeSegmentTypes.TryGetValue(segmentType, out pureCodeSegment))
                    {
                        //do nothing, we already have this segment type's value   
                    }
                    else
                    {
                        if (line.Contains("Pure code"))
                        {
                            PureCodeSegmentTypes.Add(segmentType, true);
                        }
                        else
                        {
                            PureCodeSegmentTypes.Add(segmentType, false);
                            continue;
                        }
                    }
                    //we are done processing the segment type line
                    continue;
                }

                //This check only runs on non-segment type lines
                bool pureCodeSeg = false;
                PureCodeSegmentTypes.TryGetValue(segmentType, out pureCodeSeg);
                if (pureCodeSeg == false) continue;

                //*****************************************************************
                //Processing for "pure code" segment type lines below
                //*****************************************************************

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
                            if (asmCommand.Length > 1 &&                 //asm commands are longer than 1 character
                                asmCommand.Length <= 15 &&               //asm commands are 15 characters or less
                                Char.IsNumber(asmCommand[0]) != true &&  //asm commands do not begin with a #
                                asmCommand != "db" &&                    //not valid asm command
                                asmCommand != "align" &&                 //not valid asm command
                                asmCommand != "dd" &&                    //not valid asm command
                                OnlyLcaseLettersOrNumbers(asmCommand))   //IDA asm commands are all lowercase with no 

                                yield return asmCommand;
                        }
                    }
                }
            }
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




        public static bool OnlyLcaseLettersOrNumbers(string word)
        {
            foreach (var charVal in word)
            {
                if (Char.IsLetterOrDigit(charVal) == false || Char.IsUpper(charVal))
                    return false;
            }
            return true;
        }


    }
}
