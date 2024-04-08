using KGySoft.Drawing;
using KGySoft.Drawing.Imaging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Video2SQFConverter;

internal static class Program
{
    private class MarkerColor
    {
        public char SerializedName { get; init; }
        public string MarkerName { get; init; }
        public float Red { get; init; }
        public float Green { get; init; }
        public float Blue { get; init; }
        public float Weight { get; init; }
        private Color? _color;
        public Color Color
        {
            get
            {
                if (!_color.HasValue)
                    _color = Color.FromArgb((int)(Red * 255), (int)(Green * 255), (int)(Blue * 255));
                return _color.Value;
            }
        }
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

    private static bool debug = false;
    private static IQuantizer quantizer;

    [STAThread]
    private static void Main(string[] args)
    {
        var rescaleFactor = 1;
        var frameRate = 30;

        var index = 1;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "-r":
                    rescaleFactor = int.Parse(args[index + 1]);
                    index += 2;
                    break;
                case "-f":
                    frameRate = int.Parse(args[index + 1]);
                    index += 2;
                    break;
                case "-d":
                    debug = true;
                    Directory.CreateDirectory(Path.Join(args[0], "debug"));
                    index++;
                    break;
                default:
                    break;
            }
        }

        var colors = ParseColors(args[0]);
        quantizer = PredefinedColorsQuantizer.FromCustomPalette(colors.Select(x => x.Color).ToArray());
        var (frameData, width, height) = ProcessFrame(args[0], rescaleFactor, colors);
        DeduplicateFrames(frameData);

        TextWriter writer = new StreamWriter(Path.Join(args[0], "Video.sqf"));
        writer.WriteLine("[");
        writer.WriteLine($"[{width},{height},{frameRate}],");
        writer.WriteLine("[");
        InsertColorMap(colors, writer);
        writer.WriteLine("],");
        writer.WriteLine("[");
        var first = true;
        foreach (var frame in frameData)
        {
            if (first)
                first = false;
            else
                writer.WriteLine(",");

            if (frame.DuplicateOf != -1)
                writer.Write(frame.DuplicateOf);
            else
                writer.Write("\"" + frame.Data + "\"");
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

    private static readonly List<MarkerColor> defaultColors = [new MarkerColor { SerializedName = 'b', MarkerName = "Black", Red = 0, Blue = 0, Green = 0, Weight = 0, }, new MarkerColor { SerializedName = 'w', MarkerName = "White", Red = 1, Blue = 1, Green = 1, Weight = 0, }];
    private static List<MarkerColor> ParseColors(string path)
    {
        path = Path.Join(path, "ColorConfig.json");
        if (!File.Exists(path)) return defaultColors;
        var file = File.ReadAllText(path);
        var result = JsonConvert.DeserializeObject<List<MarkerColor>>(file);
        if (result != null) return result;
        return defaultColors;
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
                frameColor[x, y] = GetColor(bytes[index + 2], bytes[index + 1], bytes[index], colors);
                index += pixelSize;
            }
            index += padding;
        }
        frame.UnlockBits(data);
        return frameColor;
    }

    private static char GetColor(byte R, byte G, byte B, List<MarkerColor> colors)
    {
        var distance = float.MaxValue;
        var result = colors[0].SerializedName;
        foreach (var color in colors)
        {
            var newDistance = Math.Abs(R - color.Color.R) + Math.Abs(G - color.Color.G) + Math.Abs(B - color.Color.B) * color.Weight;
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

            bitmap.Quantize(quantizer);

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
            if (debug)
                frame.ToBitmap(width, height, colors).Save(Path.Join(path, "debug", $"frame_{frame.Index}.png"));
        });

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