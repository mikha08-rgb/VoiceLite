using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class MetricsTracker
    {
        private readonly Settings settings;
        private readonly List<TranscriptionMetric> metrics = new List<TranscriptionMetric>();
        private readonly string metricsPath;
        private readonly string metricsDirectory;
        private readonly object lockObject = new object();

        public MetricsTracker(Settings settings)
        {
            this.settings = settings;
            metricsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoiceLite");
            metricsPath = Path.Combine(metricsDirectory, "metrics.json");

            // Ensure directory exists
            try
            {
                if (!Directory.Exists(metricsDirectory))
                {
                    Directory.CreateDirectory(metricsDirectory);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to create metrics directory", ex);
            }

            LoadMetrics();
        }

        public void RecordTranscription(
            DateTime startTime,
            TimeSpan processingTime,
            int audioLengthMs,
            string transcription,
            bool wasModifiedByDictionary,
            bool usedTemperature,
            bool usedContext,
            bool usedVAD)
        {
            if (!settings.EnableMetrics)
                return;

            try
            {
                var metric = new TranscriptionMetric
                {
                    Timestamp = startTime,
                    ProcessingTime = processingTime,
                    AudioLengthMs = audioLengthMs,
                    WordCount = CountWords(transcription),
                    WasModifiedByDictionary = wasModifiedByDictionary,
                    FeaturesUsed = new FeatureFlags
                    {
                        UsedTemperature = usedTemperature,
                        UsedContext = usedContext,
                        UsedEnhancedDictionary = settings.UseEnhancedDictionary,
                        UsedVAD = usedVAD
                    }
                };

                lock (lockObject)
                {
                    metrics.Add(metric);

                    // Keep only last 100 metrics for performance
                    if (metrics.Count > 100)
                    {
                        metrics.RemoveAt(0);
                    }
                }

                // Save periodically (every 10 transcriptions)
                if (metrics.Count % 10 == 0)
                {
                    SaveMetrics();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MetricsTracker.RecordTranscription", ex);
            }
        }

        public MetricsSummary GetSummary()
        {
            lock (lockObject)
            {
                if (!metrics.Any())
                    return new MetricsSummary();

                var recent = metrics.TakeLast(50).ToList();

                return new MetricsSummary
                {
                    TotalTranscriptions = metrics.Count,
                    AverageProcessingTimeMs = (int)recent.Average(m => m.ProcessingTime.TotalMilliseconds),
                    AverageWordsPerMinute = CalculateWPM(recent),
                    DictionaryModificationRate = recent.Count(m => m.WasModifiedByDictionary) / (float)recent.Count,
                    FeatureImpact = AnalyzeFeatureImpact(recent),
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private float CalculateWPM(List<TranscriptionMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            var avgWords = metrics.Average(m => m.WordCount);
            var avgTimeSeconds = metrics.Average(m => m.AudioLengthMs / 1000.0);

            if (avgTimeSeconds <= 0)
                return 0;

            return (float)(avgWords / avgTimeSeconds * 60);
        }

        private Dictionary<string, float> AnalyzeFeatureImpact(List<TranscriptionMetric> metrics)
        {
            var impact = new Dictionary<string, float>();

            // Calculate average processing time for each feature combination
            var withTemp = metrics.Where(m => m.FeaturesUsed.UsedTemperature).ToList();
            var withoutTemp = metrics.Where(m => !m.FeaturesUsed.UsedTemperature).ToList();

            if (withTemp.Any() && withoutTemp.Any())
            {
                var tempImprovement = withoutTemp.Average(m => m.ProcessingTime.TotalMilliseconds) -
                                    withTemp.Average(m => m.ProcessingTime.TotalMilliseconds);
                impact["Temperature"] = (float)tempImprovement;
            }

            var withContext = metrics.Where(m => m.FeaturesUsed.UsedContext).ToList();
            if (withContext.Any())
            {
                impact["Context"] = withContext.Count(m => !m.WasModifiedByDictionary) / (float)withContext.Count;
            }

            var withVAD = metrics.Where(m => m.FeaturesUsed.UsedVAD).ToList();
            if (withVAD.Any())
            {
                impact["VAD"] = (float)withVAD.Average(m => m.ProcessingTime.TotalMilliseconds);
            }

            return impact;
        }

        private void LoadMetrics()
        {
            try
            {
                if (File.Exists(metricsPath))
                {
                    var json = File.ReadAllText(metricsPath);
                    var loaded = JsonSerializer.Deserialize<List<TranscriptionMetric>>(json);
                    if (loaded != null)
                    {
                        lock (lockObject)
                        {
                            metrics.Clear();
                            metrics.AddRange(loaded.TakeLast(100)); // Keep only recent metrics
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MetricsTracker.LoadMetrics", ex);
            }
        }

        private void SaveMetrics()
        {
            try
            {
                lock (lockObject)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var json = JsonSerializer.Serialize(metrics, options);
                    File.WriteAllText(metricsPath, json);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MetricsTracker.SaveMetrics", ex);
            }
        }

        public void GenerateReport()
        {
            try
            {
                var summary = GetSummary();
                var reportPath = Path.Combine(metricsDirectory,
                    $"metrics_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var report = $@"VoiceLite Metrics Report
========================
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Summary:
--------
Total Transcriptions: {summary.TotalTranscriptions}
Average Processing Time: {summary.AverageProcessingTimeMs}ms
Average Words Per Minute: {summary.AverageWordsPerMinute:F1}
Dictionary Modification Rate: {summary.DictionaryModificationRate:P}

Feature Impact:
--------------";

                foreach (var feature in summary.FeatureImpact)
                {
                    report += $"\n{feature.Key}: {feature.Value:F2}";
                }

                report += @"

Accuracy Improvements:
---------------------
Temperature Optimization: " + (settings.UseTemperatureOptimization ? "ENABLED" : "DISABLED") + @"
Context Prompt: " + (settings.UseContextPrompt ? "ENABLED" : "DISABLED") + @"
Enhanced Dictionary: " + (settings.UseEnhancedDictionary ? "ENABLED" : "DISABLED") + @"
Voice Activity Detection: " + (settings.UseVAD ? "ENABLED" : "DISABLED") + @"

Notes:
------
- Lower processing times indicate better performance
- Lower dictionary modification rates suggest better initial accuracy
- Monitor these metrics after enabling new features
";

                File.WriteAllText(reportPath, report);
                ErrorLogger.LogMessage($"Metrics report saved to: {reportPath}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MetricsTracker.GenerateReport", ex);
            }
        }
    }

    public class TranscriptionMetric
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public int AudioLengthMs { get; set; }
        public int WordCount { get; set; }
        public bool WasModifiedByDictionary { get; set; }
        public FeatureFlags FeaturesUsed { get; set; } = new FeatureFlags();
    }

    public class FeatureFlags
    {
        public bool UsedTemperature { get; set; }
        public bool UsedContext { get; set; }
        public bool UsedEnhancedDictionary { get; set; }
        public bool UsedVAD { get; set; }
    }

    public class MetricsSummary
    {
        public int TotalTranscriptions { get; set; }
        public int AverageProcessingTimeMs { get; set; }
        public float AverageWordsPerMinute { get; set; }
        public float DictionaryModificationRate { get; set; }
        public Dictionary<string, float> FeatureImpact { get; set; } = new Dictionary<string, float>();
        public DateTime LastUpdated { get; set; }
    }
}