using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;


namespace BadAppleSQFConverter;

internal static class Program
{
    public class Frame
    {
        public int index;
        public int duplicateOf = -1;
        public string data = "";
        public void Compress()
        {
            var result = new StringBuilder();
            var count = 1;
            data += " ";
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (data[i] == data[i + 1])
                {
                    count++;
                }
                else
                {
                    result.Append(count);
                    result.Append(data[i]);
                    count = 1;
                }
            }
            data = result.ToString();
        }

        public Bitmap ToBitmap(int width, int height)
        {
            var regexNumberValues = new Regex(@"[0-9]+");
            var regexCharValues = new Regex(@"[a-z]");

            var numberMatches = regexNumberValues.Matches(data).ToList();
            var charMatches = regexCharValues.Matches(data).ToList();

            var result = new Bitmap(width, height);
            var x = 0;
            var y = 0;

            for (int i = 0; i < numberMatches.Count; i++)
            {
                var number = int.Parse(numberMatches[i].Value);
                var color = charMatches[i].Value == "t" ? Color.White : Color.Black;
                for (int j = 0; j < number; j++)
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

        if (args.Length >= 2)
            rescaleFactor = int.Parse(args[1]);

        var (frameData, width, height) = ProcessFrame(args[0], rescaleFactor);
        DeduplicateFrames(frameData);

        TextWriter writer = new StreamWriter(Path.Join(args[0], "BadApple.sqf"));
        writer.Write("[");
        writer.Write($"[{width},{height}], [");
        var first = true;
        foreach (var frame in frameData)
        {
            if (first)
                first = false;
            else
                writer.Write(",");

            if (frame.duplicateOf != -1)
                writer.Write(frame.duplicateOf);
            else
                writer.Write("\"" + frame.data + "\"");
            // frame.ToBitmap(width, height).Save(Path.Join(args[0], "debug", $"frame_{frame.index}.png"));
        }
        writer.Write("]]");
        writer.Flush();
        writer.Close();
    }

    private static bool[,] GetFrameColors(Bitmap frame)
    {
        var data = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixelSize = data.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3; // only works with 32 or 24 pixel-size bitmap!
        var padding = data.Stride - (data.Width * pixelSize);
        var bytes = new byte[data.Height * data.Stride];

        var frameColor = new bool[frame.Width, frame.Height];

        // copy the bytes from bitmap to array
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        var index = 0;
        
        for (var x = 0; x < frame.Width; x++)
        {
            for (var y = 0; y < frame.Height; y++)
            {
                frameColor[x, y] = IsOn(bytes[index+2], bytes[index+1], bytes[index]);
                index += pixelSize;
            }
            index += padding;
        }
        frame.UnlockBits(data);
        return frameColor;
    }

    private static bool IsOn(byte R, byte G, byte B)
    {
        return R + G + B > 382.5f;
    }

    private static (List<Frame>, int, int) ProcessFrame(string path, int rescaleFactor)
    {
        ConcurrentBag<Frame> result = new ConcurrentBag<Frame>();
        var files = Directory.GetFiles(path, "*.png").OrderBy(x => int.Parse(x.Replace(path, "").Replace(".png", "").Replace("bad_apple_", ""))).ToList();
        var firstFrame = new Bitmap(files[0]);
        
        int width = firstFrame.Width / rescaleFactor;
        int height = firstFrame.Height / rescaleFactor;

        // Parallel.For(0, files.Count, i =>
        for (int i = 0; i < files.Count; i++)
        {
            var frameStr = new StringBuilder();

            var bitmap = new Bitmap(files[i]);

            if (rescaleFactor != 1)
                bitmap = new Bitmap(bitmap, width, height);

            var frameColors = GetFrameColors(bitmap);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    frameStr.Append(frameColors[x, y] ? "t" : "f");
                }
            }
            var frame = new Frame
            {
                index = i,
                data = frameStr.ToString()
            };
            frame.Compress();
            result.Add(frame);
            Console.WriteLine($"Processed frame {frame.index} {result.Count}/6562");
        }
        // );

        return (result.OrderBy(x => x.index).ToList(), width, height);
    }

    private static void DeduplicateFrames(List<Frame> frames)
    {
        var cache = new Dictionary<string, int>();
        foreach (var frame in frames)
        {
            if (cache.TryGetValue(frame.data, out int value))
            {
                frame.duplicateOf = value;
            }
            else
            {
                cache.Add(frame.data, frame.index);
            }
        }
    }
}