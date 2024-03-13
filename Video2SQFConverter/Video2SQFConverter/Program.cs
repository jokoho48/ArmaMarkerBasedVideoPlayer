using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Video2SQFConverter;

internal static class Program
{
    private struct MarkerColor
    {
        public char SerializedName { get; init; }
        public string MarkerName { get; init; }
        public float Red { get; init; }
        public float Green { get; init; }
        public float Blue { get; init; }
        public Color Color => Color.FromArgb((int)(Red * 255), (int)(Green * 255), (int)(Blue * 255));
    }
    private class Frame
    {
        public int Index;
        public int DuplicateOf = -1;
        public string Data = "";
        public void Compress()
        {
            var result = new StringBuilder();
            var count = 1;
            Data += " ";
            for (var i = 0; i < Data.Length - 1; i++)
            {
                if (Data[i] == Data[i + 1])
                {
                    count++;
                }
                else
                {
                    result.Append(count);
                    result.Append(Data[i]);
                    count = 1;
                }
            }
            Data = result.ToString();
        }

        public Bitmap ToBitmap(int width, int height, List<MarkerColor> colors)
        {
            var regexNumberValues = new Regex(@"[0-9]+");
            var regexCharValues = new Regex(@"[a-z]");

            var numberMatches = regexNumberValues.Matches(Data).ToList();
            var charMatches = regexCharValues.Matches(Data).ToList();

            var result = new Bitmap(width, height);
            var x = 0;
            var y = 0;

            for (var i = 0; i < numberMatches.Count; i++)
            {
                var number = int.Parse(numberMatches[i].Value);
                var color = colors.Find(mc => charMatches[i].Value[0] == mc.SerializedName).Color;
                for (var j = 0; j < number; j++)
                {
                    result.SetPixel(x, y, color);
                    x++;
                    if (x >= width)
                    {
                        x = 0;
                        y++;
                    }
                }
            }
            return result;
        }
    }
    
    [STAThread]
    private static void Main(string[] args)
    {
        var rescaleFactor = 1;
        var frameRate = 30;
        if (args.Length >= 2)
            rescaleFactor = int.Parse(args[1]);
        if (args.Length >= 3)
            frameRate = int.Parse(args[2]);
        var colors = ParseColors(args[0]);
        var (frameData, width, height) = ProcessFrame(args[0], rescaleFactor, colors);
        DeduplicateFrames(frameData);

        TextWriter writer = new StreamWriter(Path.Join(args[0], "Video.sqf"));
        writer.Write("[");
        writer.Write($"[{width},{height},{frameRate}],");
        writer.Write("[");
        InsertColorMap(colors, writer);
        writer.Write("],");
        writer.Write("[");
        var first = true;
        foreach (var frame in frameData)
        {
            if (first)
                first = false;
            else
                writer.Write(",");

            if (frame.DuplicateOf != -1)
                writer.Write(frame.DuplicateOf);
            else
                writer.Write("\"" + frame.Data + "\"");
            frame.ToBitmap(width, height, colors).Save(Path.Join(args[0], "debug", $"frame_{frame.Index}.png"));
        }
        writer.Write("]]");
        writer.Flush();
        writer.Close();
    }

    private static void InsertColorMap(List<MarkerColor> colors, TextWriter writer)
    {
        var first = true;
        foreach (var color in colors)
        {
            if (first)
                first = false;
            else
                writer.Write(",");
            
            writer.Write($"[\"{color.SerializedName}\", \"{color.MarkerName}\"]");
        }
    }
    private static List<MarkerColor> ParseColors(string path)
    {
        var file = File.ReadAllText(Path.Join(path, "ColorConfig.json"));
        var result = JsonConvert.DeserializeObject<List<MarkerColor>>(file);
        if (result != null) return result;
        return
        [
            new MarkerColor
            {
                SerializedName = 'b',
                MarkerName = "Black",
                Red = 0,
                Blue = 0,
                Green = 0
            },

            new MarkerColor
            {
                SerializedName = 'w',
                MarkerName = "White",
                Red = 1,
                Blue = 1,
                Green = 1
            }
        ];
    }
    
    private static char[,] GetFrameColors(Bitmap frame, List<MarkerColor> colors)
    {
        var width = frame.Width;
        var height = frame.Height;
        var data = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixelSize = data.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3; // only works with 32 or 24 pixel-size bitmap!
        var padding = data.Stride - (data.Width * pixelSize);
        var bytes = new byte[data.Height * data.Stride];

        var frameColor = new char[width, height];

        // copy the bytes from bitmap to array
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        var index = 0;
        
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                frameColor[x, y] = IsOn(bytes[index+2], bytes[index+1], bytes[index], colors);
                index += pixelSize;
            }
            index += padding;
        }
        frame.UnlockBits(data);
        return frameColor;
    }

    private static char IsOn(byte R, byte G, byte B, List<MarkerColor> colors)
    {
        var distance = float.MaxValue;
        var result = 'b';
        foreach (var color in colors)
        {
            var newDistance = Math.Abs(R - color.Color.R) + Math.Abs(G - color.Color.G) + Math.Abs(B - color.Color.B);
            if (!(newDistance < distance)) continue;
            distance = newDistance;
            result = color.SerializedName;
        }
        return result;
    }

    private static (List<Frame>, int, int) ProcessFrame(string path, int rescaleFactor, List<MarkerColor> colors)
    {
        var result = new ConcurrentBag<Frame>();
        var files = Directory.GetFiles(path, "*.png").OrderBy(x => int.Parse(x.Replace(path, "").Replace("\\", "").Replace(".png", ""))).ToList();
        var firstFrame = new Bitmap(files[0]);
        
        var width = firstFrame.Width / rescaleFactor;
        var height = firstFrame.Height / rescaleFactor;
        
        Parallel.For(0, files.Count, i =>
        //for (int i = 0; i < files.Count; i++)
        {
            var frameStr = new StringBuilder();

            var bitmap = new Bitmap(files[i]);

            if (rescaleFactor != 1)
                bitmap = new Bitmap(bitmap, width, height);

            var frameColors = GetFrameColors(bitmap, colors);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    frameStr.Append(frameColors[x, y]);
                }
            }
            var frame = new Frame
            {
                Index = i,
                Data = frameStr.ToString()
            };
            frame.Compress();
            result.Add(frame);
            Console.WriteLine($"Processed frame {frame.Index} {result.Count}/{files.Count}");
        }
        );

        return (result.OrderBy(x => x.Index).ToList(), width, height);
    }

    private static void DeduplicateFrames(List<Frame> frames)
    {
        var cache = new Dictionary<string, int>();
        foreach (var frame in frames)
        {
            if (cache.TryGetValue(frame.Data, out var value))
            {
                frame.DuplicateOf = value;
            }
            else
            {
                cache.Add(frame.Data, frame.Index);
            }
        }
    }
}