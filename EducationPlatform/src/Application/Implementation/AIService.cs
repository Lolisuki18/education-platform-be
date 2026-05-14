using System.Text;
using Application.Interface;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Application.Implementation
{
    public class AIService : IAIService
    {
        #region Attributes
        private readonly HttpClient httpClient;
        #endregion

        #region Properties
        #endregion

        public AIService()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var aiLocal = configuration["AI:LocalPath"]
                ?? throw new InvalidOperationException("AI:LocalPath missing");

            httpClient = new HttpClient
            {
                BaseAddress = new Uri(aiLocal),
                Timeout = TimeSpan.FromMinutes(2)
            };
        }

        #region Methods
        public async Task<string> GenerateQuizAsync(
      string grade,
      string subject,
      string transcript)
        {
            var cleanGrade = grade.Replace("Grade ", "").Trim();
            var prompt = $$"""
                B?n là giáo viên {{subject}} l?p {{cleanGrade}} t?i Vi?t Nam.
                Nhi?m v?:
                T?o CHÍNH XÁC 10 câu h?i tr?c nghi?m giúp h?c sinh ôn t?p.
                Ng? c?nh bài h?c:
                {{transcript}}
                Yêu c?u:
                - Câu h?i phù h?p trình d? l?p {{cleanGrade}}
                - Bám sát n?i dung bài h?c
                - M?i câu có 4 phuong án tr? l?i d?y d? n?i dung (KHÔNG ph?i ch? cái A, B, C, D)
                - Ch? có 1 dáp án dúng
                - Tru?ng "answer" ch?a N?I DUNG dáp án dúng (gi?ng h?t m?t trong các options)
                - Có gi?i thích ng?n g?n
                QUAN TR?NG:
                - Tr? v? DUY NH?T JSON ARRAY
                - Không markdown, không gi?i thích ngoài JSON
                - M?i option ph?i là câu tr? l?i hoàn ch?nh, KHÔNG PH?I ch? cái
                Ví d? dúng:
                [
                  {
                    "question": "Th? dô c?a Vi?t Nam là gì?",
                    "options": ["Hà N?i", "H? Chí Minh", "Ðà N?ng", "Hu?"],
                    "answer": "Hà N?i",
                    "explanation": "Hà N?i là th? dô c?a Vi?t Nam t? nam 1945."
                  }
                ]
                Ví d? SAI (không làm theo):
                {
                  "options": ["A", "B", "C", "D"],
                  "answer": "A"
                }
                """;

            var body = new
            {
                model = "gemma3:4b",
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.3
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "http://localhost:11434/api/generate");

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseString);

            var parsed = JObject.Parse(responseString);
            var aiText = parsed["response"]?.ToString() ?? "";

            // Clean AI output
            aiText = aiText.Replace("```json", "").Replace("```", "").Trim();

            var start = aiText.IndexOf('[');
            var end = aiText.LastIndexOf(']');

            if (start != -1 && end != -1)
                aiText = aiText.Substring(start, end - start + 1);

            // Validate JSON
            try
            {
                var quizArray = JArray.Parse(aiText);
                return quizArray.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                throw new Exception("AI returned invalid JSON format.");
            }
        }
        #endregion
    }

}


