using Application.Interface;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Application.Implementation
{
    public class SpeechToTextService : ISpeechToTextService
    {
        #region Attributes
        private readonly string ffmpegPath = "ffmpeg";
        private readonly string whisperPath = "whisper-cli";
        private readonly string modelPath;
        #endregion

        #region Properties
        #endregion

        public SpeechToTextService()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            ffmpegPath = configuration["FFmpeg:Path"]
                ?? throw new InvalidOperationException("FFmpeg:Path missing");

            whisperPath = configuration["Whisper:Path"]
                ?? throw new InvalidOperationException("Whisper:Path missing");

            modelPath = configuration["Whisper:ModelPath"]
                ?? throw new InvalidOperationException("Whisper:ModelPath missing");
        }

        #region Methods
        public async Task<string> TranscribeVideo(string videoFullPath)
        {
            Console.WriteLine("Extracting audio...");
            var audioPath = Path.ChangeExtension(videoFullPath, ".wav");
            await RunProcess(
                ffmpegPath,
                $"-y -i \"{videoFullPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{audioPath}\""
            );

            Console.WriteLine("Splitting audio...");
            var chunks = await SplitAudio(audioPath, 30); // 30s per chunk

            Console.WriteLine($"Transcribing {chunks.Count} chunks in parallel...");
            var transcript = await TranscribeChunksParallel(chunks);

            // Save transcript
            var transcriptPath = Path.ChangeExtension(videoFullPath, ".txt");
            await File.WriteAllTextAsync(transcriptPath, transcript);

            // cleanup
            TryDelete(audioPath);
            foreach (var chunk in chunks)
            {
                TryDelete(chunk);
                TryDelete(chunk + ".txt");
            }

            var chunkDir = Path.Combine(Path.GetDirectoryName(audioPath)!, "chunks");
            if (Directory.Exists(chunkDir))
            {
                try { Directory.Delete(chunkDir, true); } catch { }
            }

            return transcriptPath;
        }

        private async Task<string> TranscribeChunksParallel(List<string> chunks)
        {
            var semaphore = new SemaphoreSlim(3); // max 3 parallel processes

            var tasks = chunks.Select(async chunk =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var outputTxt = chunk + ".txt";

                    await RunProcess(
                        whisperPath,
                        $"-m \"{modelPath}\" -f \"{chunk}\" -otxt -t 4 -l vi"
                    );

                    return await File.ReadAllTextAsync(outputTxt);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            return string.Join("\n", results);
        }

        private async Task<List<string>> SplitAudio(string audioPath, int segmentSeconds)
        {
            var outputDir = Path.Combine(Path.GetDirectoryName(audioPath)!, "chunks");
            Directory.CreateDirectory(outputDir);

            var pattern = Path.Combine(outputDir, "chunk_%03d.wav");

            await RunProcess(
                ffmpegPath,
                $"-i \"{audioPath}\" -f segment -segment_time {segmentSeconds} -c copy \"{pattern}\""
            );

            return Directory.GetFiles(outputDir, "chunk_*.wav")
                            .OrderBy(f => f)
                            .ToList();
        }

        private async Task RunProcess(string fileName, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"Process failed: {fileName}\nArgs: {arguments}\nError: {error}\nOutput: {output}"
                );
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
        #endregion
    }
}


