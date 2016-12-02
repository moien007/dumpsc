using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Diagnostics;

namespace dumpsc
{
    class Program
    {
        static void Main(string[] args)
        {
            Directory.CreateDirectory("input");
            Directory.CreateDirectory("output");

            long elapsedMilliseconds = 0;

            foreach (string fileName in Directory.GetFiles("input"))
            {
                Stopwatch watch = Stopwatch.StartNew();
                watch.Start();

                Console.WriteLine("Processing {0}", fileName);
                Console.WriteLine("\tDecompressing...");
                byte[] decodedBytes = Decompress(fileName);

                Console.WriteLine("\tDecoding...");
                Bitmap[] images = Decode(decodedBytes);

                Console.WriteLine("\tSaving Images...");

                string savePath = @"output\" + Path.GetFileNameWithoutExtension(fileName) + @"\";

                Directory.CreateDirectory(savePath);

                for (int i = 0; i < images.Length; i++)
                {
                    using(FileStream fileStream = new FileStream(savePath + i.ToString() + ".png", FileMode.Create))
                    {
                        images[i].Save(fileStream, ImageFormat.Png);
                    }    
                }

                watch.Stop();
                Console.WriteLine("\tDone in {0}ms", watch.ElapsedMilliseconds);

                elapsedMilliseconds += watch.ElapsedMilliseconds;
            }

            Console.WriteLine("\nFinished in {0}ms", elapsedMilliseconds);
            Console.ReadKey();
        }

        public static Bitmap[] Decode(byte[] decodedBytes)
        {
            List<Bitmap> images = new List<Bitmap>();

            using (MemoryStream memory = new MemoryStream(decodedBytes))
            using (BinaryReader binaryReader = new BinaryReader(memory))
            {
                while (memory.Length - memory.Position > 5)
                {
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

                    Bitmap bmp = new Bitmap(scHeader.Width, scHeader.Height, PixelFormat.Format32bppArgb);

                    List<Color> pixels = new List<Color>();

                    for (int x = 0; x < bmp.Width; x++)
                    {
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            Color pixel = ConvertToPixelColor(binaryReader.ReadBytes(pixelSize), scHeader.SubType);
                            pixels.Add(pixel);
                            bmp.SetPixel(x, y, pixel);
                        }
                    }

                    
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
                       /* for (int j = 0; j < (scHeader.Height % 32); j++)
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
            // read file bytes, then skep 26 bytes (idk why)
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

            switch (subType)
            {
                case 0: // RGB8888
                    return Color.FromArgb((int)BitConverter.ToUInt32(pixel, 0));
                case 2: // RGB4444
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb(((pix >> 12) & 0xF) << 4, ((pix >> 8) & 0xF) << 4, ((pix >> 4) & 0xF) << 4, ((pix >> 0) & 0xF) << 4);
                case 4: // RGB565
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb(((pix >> 11) & 0x1F) << 3, ((pix >> 5) & 0x3F) << 2, (pix & 0x1F) << 3);
                case 6: // RGB555?
                    pix = BitConverter.ToUInt16(pixel, 0);
                    return Color.FromArgb((pix >> 16) & 0x80, (pix >> 9) & 0x7C, (pix >> 6) & 0x3E, (pix >> 3) & 0x1F);
                case 10: //BGR233?
                    pix = pixel.FirstOrDefault();
                    return Color.FromArgb((pix) & 0x3, ((pix >> 2) & 0x7) << 2, ((pix >> 5) & 0x7) << 5);
            }

            throw new Exception("Unknown pixel color " + subType.ToString());
        }
    }

    public struct SCHeader
    {
        public byte FileType;
        public uint FileSize;
        public byte SubType;
        public ushort Width;
        public ushort Height;
    }
}
