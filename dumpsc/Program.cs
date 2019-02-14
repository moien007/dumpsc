/*
 * Project : SC Extractor for Clash Royal
 * Author : Moien007 (https://github.com/moien007)
 * 
 * Description : Port https://github.com/123456abcdef/cr-sc-dump to C#
 * 
 * TODO :
 * [-] Reverse Code for Edit _tex.sc files
 * [-] Fix TODO #1
 */

using System;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Diagnostics;

/*
 * How to use :
 * after compile, but .exe to empty folder and run
 * program generates two folder named as 'input' and 'output'
 * open clash royal apk file with WinRAR or 7Zip, goto assets\sc folder and pickup any _tex.sc files
 * put _tex.sc files to input folder and run .exe
 * wait a minute
 * open output folder
 * DONE!!!
 */

namespace dumpsc
{
    class Program
    {
        public const string InputFolder = "input";
        public const string OutputFolder = "output";

        static void Main(string[] args)
        {
            // create input and output directories
            Directory.CreateDirectory(InputFolder);
            Directory.CreateDirectory(OutputFolder);

            long elapsedMilliseconds = 0;

            // for each file in input folder
            foreach (string fileName in Directory.GetFiles(InputFolder))
            {
                // create stop for calculate execution time
                Stopwatch watch = Stopwatch.StartNew();
                watch.Start();

                Console.WriteLine("Processing {0}", fileName);
                Console.WriteLine("\tDecompressing...");
                byte[] decodedBytes = Decompress(fileName); // load and decompress file

                Console.WriteLine("\tDecoding...");
                Bitmap[] images = Decode(decodedBytes);

                Console.WriteLine("\tSaving Images...");

                string savePath = string.Format("{0}\\{1}", OutputFolder, Path.GetFileNameWithoutExtension(fileName));

                for (int i = 0; i < images.Length; i++)
                {
                    using (FileStream fileStream = new FileStream(string.Format("\\{0}.png", savePath, i), FileMode.Create))
                    {
                        // save bitmap with png image format to file
                        images[i].Save(fileStream, ImageFormat.Png);
                    }    
                }

                watch.Stop();
                Console.WriteLine("\tDone in {0}ms", watch.ElapsedMilliseconds);

                elapsedMilliseconds += watch.ElapsedMilliseconds;
            }

            Console.WriteLine("\nFinished in {0}ms", elapsedMilliseconds);
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
        }

        public static Bitmap[] Decode(byte[] decodedBytes)
        {
            // textures in sc files
            List<Bitmap> images = new List<Bitmap>();

            using (MemoryStream memory = new MemoryStream(decodedBytes))
            using (BinaryReader binaryReader = new BinaryReader(memory))
            {
                while (memory.Length - memory.Position > 5)
                {
                    // read sc header
                    SCHeader scHeader = ReadSCHeader(binaryReader);

                    Console.WriteLine("\t FileType: {0}, FileSize: {1}, SubType: {2}, Width: {3}, Height: {4}",
                                    scHeader.FileType, scHeader.FileSize, scHeader.SubType, scHeader.Width, scHeader.Height);
                    int pixelSize;

                    switch(scHeader.SubType)
                    {
                        case 0:
                            pixelSize = 4;
                            break;
                        case 2:
                        case 4:
                        case 6:
                            pixelSize = 2;
                            break;
                        case 10:
                            pixelSize = 1;
                            break;
                        default:
                            throw new Exception("Unknown pixel type " + scHeader.SubType.ToString());
                    }

                    // create bitmap image using width and height from sc header
                    Bitmap bmp = new Bitmap(scHeader.Width, scHeader.Height, PixelFormat.Format32bppArgb);

                    // we should lock bitmap ? 

                    List<Color> pixels = new List<Color>();

                    for (int x = 0; x < bmp.Width; x++)
                    {
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            // get pixel from bytes
                            Color pixel = ConvertToPixelColor(binaryReader.ReadBytes(pixelSize), scHeader.SubType);

                            // add it to list
                            pixels.Add(pixel);

                            // and set it to bitmap
                            bmp.SetPixel(x, y, pixel);
                        }
                    }

                    // from this part all of codes just converted to csharp with comments                    
                    if(scHeader.FileType == 27 || scHeader.FileType == 28)
                    {
                        int iSrcPix = 0;

                        for (int l = 0; l < Math.Floor((decimal)scHeader.Height / 32); l++) //block of 32 lines
                        {
                            // normal 32-pixels blocks
                            for (int k = 0; k < Math.Floor((decimal)scHeader.Width / 32); k++) // 32-pixels blocks in a line
                            {
                                for (int j = 0; j < 32; j++) // line in a multi line block
                                {
                                    for (int h = 0; h < 32; h++) // pixels in a block
                                    {
                                        bmp.SetPixel((h + (k * 32)), (j + (l * 32)), pixels[iSrcPix]);

                                        iSrcPix++;
                                    }
                                }
                            }

                            // line end blocks
                            for (int j = 0; j < 32; j++)
                            {
                                for (int h = 0; h < scHeader.Width % 32; h++)
                                {
                                    bmp.SetPixel((h + (scHeader.Width - (scHeader.Width % 32))), (j + (l * 32)), pixels[iSrcPix]);
                                    iSrcPix++;
                                }
                            }
                        }

                        // final lines
                        for (int k = 0; k < Math.Floor((decimal)scHeader.Width / 32); k++) // 32-pixels blocks in a line
                        {
                            for (int j = 0; j < (scHeader.Height % 32); j++) // line in a multi line block
                            {
                                for (int h = 0; h < 32; h++) // pixels in a 32-pixels-block
                                {
                                    bmp.SetPixel((h + (k * 32)), (j + (scHeader.Height - (scHeader.Height % 32))), pixels[iSrcPix]);
                                    iSrcPix++;
                                }
                            }
                        }

                        // line end blocks
                       /* for (int j = 0; j < (scHeader.Height % 32); j++) // TODO #1 we got error here about at runtime, i will working on it
                        {
                            for (int h = 0; h < (scHeader.Width & 32); h++)
                            {
                                bmp.SetPixel((h + (scHeader.Width - (scHeader.Width % 32))), (j + (scHeader.Height - (scHeader.Height % 32))), pixels[iSrcPix]);
                                iSrcPix++;
                            }
                        } */
                    }

                    images.Add(bmp);
                }
            }

            return images.ToArray();
        }


        public static byte[] Decompress(string filePath)
        {
            // read file bytes, then skip first 26 bytes (idk why)
            byte[] fileBytes = File.ReadAllBytes(filePath).Skip(26).ToArray();

            byte[] decompressed;

            using (MemoryStream memory = new MemoryStream())
            using (BinaryWriter binaryWriter = new BinaryWriter(memory))
            {
                // add first 9 bytes
                binaryWriter.Write(fileBytes, 0, 9);

                // add four zero bytes (fix header) 
                binaryWriter.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });

                // add another bytes
                binaryWriter.Write(fileBytes, 9, fileBytes.Length - 9);

                // get bytes from memory and then decompress using LZMA
                decompressed = LZMA.Decompress(memory.ToArray());
            }

            return decompressed;
        }

        // just for saving time :D
        public static SCHeader ReadSCHeader(BinaryReader reader)
        {
            return new SCHeader
            {
                FileType = reader.ReadByte(),
                FileSize = reader.ReadUInt32(),
                SubType = reader.ReadByte(),
                Width = reader.ReadUInt16(),
                Height = reader.ReadUInt16(),
            };
        }

        public static Color ConvertToPixelColor(byte[] pixel, byte subType)
        {
            ushort pix;

            switch (subType) // this part just converted to csharp
            {
                case 0: // RGB8888
                    return Color.FromArgb((int)BitConverter.ToUInt32(pixel, 0));
                case 2: // RGB4444
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb(((pix >> 12) & 0xF) << 4, ((pix >> 8) & 0xF) << 4, ((pix >> 4) & 0xF) << 4, ((pix >> 0) & 0xF) << 4);
                case 4: // RGB565
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb(((pix >> 11) & 0x1F) << 3, ((pix >> 5) & 0x3F) << 2, (pix & 0x1F) << 3);
                case 6: // LA88
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb((pix >> 8), (pix >> 8), (pix >> 8), (pix & 0xFF));
                case 10: //L8
                    pix = pixel.FirstOrDefault();
                    return Color.FromArgb(pix,pix,pix);
            }

            throw new Exception("Unknown pixel color " + subType.ToString());
        }
    }

    public class SCHeader
    {
        public byte FileType { get; set; }
        public uint FileSize { get; set; }
        public byte SubType { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
    }
}
