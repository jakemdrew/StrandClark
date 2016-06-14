using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bytesFileToSequence
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Input Directory:");
            string inputDir = Console.ReadLine();

            int fcount = 0;
            string[] bytesfiles = Directory.GetFiles(inputDir, "*.bytes");
            StringBuilder bytesSequence = new StringBuilder();

            foreach (var path in bytesfiles)
            {   //Convert each file line to sequence data
                foreach (var seqData in ReadHexAsSequence(path))
                {
                    bytesSequence.Append(seqData);
                }
                //Create the sequence file
                string fileName = Path.GetFileNameWithoutExtension(path);
                File.WriteAllText(inputDir + "\\" + fileName + ".bytesseq", bytesSequence.ToString());
                fcount++;
            }

            Console.WriteLine("New Sequence Files Created: " + fcount);
            Console.ReadLine();
        }

        public static IEnumerable<string> ReadHexAsSequence(string filePath)
        {
            foreach (var line in File.ReadLines(filePath))
            {
                //Remove the line number, spaces, and ?? values from each line of hex in the .bytes file.
                string hexLine = line.Substring(9).Replace(" ", string.Empty).Replace("?", string.Empty);
                //Now convert each single hex character to a 2 character sequence
                string sequenceData = HexLineToSequenceLine(hexLine);
                //return each line from the input file as it is created.
                yield return sequenceData;
            }
        }


        public static string HexLineToSequenceLine(string hexLine)
        {
            StringBuilder sequenceLine = new StringBuilder();
            foreach (char hexChar in hexLine)
            {
                sequenceLine.Append(HexToSequence(hexChar));
            }
            return sequenceLine.ToString();
        }

        /// <summary>
        /// Converts a single hex character into a 4 character gene sequence
        /// </summary>
        /// <param name="hexChar"></param>
        /// <returns></returns>
        public static string HexToSequence(char hexChar)
        {
            //Assumes 16 valid characters 0-9, A-F
            switch (hexChar)
            {
                case '0':
                    return "AA";
                case '1':
                    return "AC";
                case '2':
                    return "AG";
                case '3':
                    return "AT";
                case '4':
                    return "CA";
                case '5':
                    return "CC";
                case '6':
                    return "CG";
                case '7':
                    return "CT";
                case '8':
                    return "GA";
                case '9':
                    return "GC";
                case 'A':
                    return "GG";
                case 'B':
                    return "GT";
                case 'C':
                    return "TA";
                case 'D':
                    return "TC";
                case 'E':
                    return "TG";
                case 'F':
                    return "TT";
                default:
                    throw new Exception("Character not supported!  Please input 0-9, A-F");
            }
        }
    }
}
