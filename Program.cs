using System;
using Mscc.GenerativeAI;
using Microsoft.Extensions.Configuration;

namespace TranslationApp
{

    class Program
    {
        const int MAX_ITERATIONS = 10;
        const int MAX_ATTEMPTS = 3;
        const int BATCH_SIZE = 15;
        const int PAUSE = 2000;

        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string api = configuration["GeminiApiKey"];

            if (string.IsNullOrEmpty(api))
            {
                Console.WriteLine("Error - API key not found.");
                return;
            }

            GoogleAI googleAI = new GoogleAI(api);
            GenerativeModel model = googleAI.GenerativeModel("models/gemini-1.5-flash");
            
            Console.WriteLine("TRANSLATOR");
            Console.WriteLine("Enter English text line by line. When finished, type 'OK'.");
            Console.WriteLine($"You have a maximum of {MAX_ITERATIONS} iterations.\n");

            int iterationCount = 0;

            while (iterationCount < MAX_ITERATIONS)
            {
                iterationCount++;

                Console.Clear();

                Console.WriteLine($"Iteration {iterationCount}/{MAX_ITERATIONS}");
                Console.WriteLine("Enter terms (type 'OK' when finished):\n");

                List<string> terms = new List<string>();

                while (true)
                {
                    string input = Console.ReadLine()?.Trim();

                    if (input?.ToUpper() == "OK")
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        terms.Add(input.Trim());
                    }
                }

                if (terms.Count == 0)
                {
                    Console.WriteLine("No terms entered.");
                    continue;
                }

                for (int i = 0; i < terms.Count; i += BATCH_SIZE)
                {
                    List<string> batch = terms.Skip(i).Take(BATCH_SIZE).ToList();
                    int batchNumber = (i / BATCH_SIZE) + 1;

                    bool success = false;
                    int attemptCount = 0;

                    while (!success && attemptCount < MAX_ATTEMPTS)
                    {
                        attemptCount++;
                        try
                        {
                            string recnik = @"";
                            string prompt = $@"
                                            You are an expert translator for power tools and machinery.
                                            {recnik}
                                            
                                            TASK:
                                            Translate the following technical terms from English to Serbian using the terminology above when applicable.
                                            Respond ONLY in the format: 'all English terms', then one empty line, then 'all Serbian translations'.
                                            Each term must be on a separate line.
                                            If the English terms are numbered, keep the SAME numbering in Serbian.
                                            If there are dots, asterisks, or similar symbols at the beginning of a line, IGNORE them.

                                            TERMS TO TRANSLATE:
                                            {string.Join("\n", batch)}";
                            Console.WriteLine("Generating translation...");
                            var response = await model.GenerateContent(prompt);
                            Console.WriteLine("Response received!");

                            Console.WriteLine(response.Text);

                            success = true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error. Attempt {attemptCount}/{MAX_ATTEMPTS}.");
                            Console.WriteLine(e.Message);

                            if (attemptCount < MAX_ATTEMPTS)
                            {
                                await Task.Delay(2000 * attemptCount);
                            }
                        }
                    }

                    if (!success)
                    {
                        Console.WriteLine($"Failed for batch number {batchNumber}");
                    }

                    if (i + BATCH_SIZE < terms.Count)
                    {
                        await Task.Delay(PAUSE);
                    }
                }
                Console.WriteLine("Translation finished.\nOptions:");
                Console.WriteLine("ENTER - new translation");
                Console.WriteLine("'exit' - quit");
                Console.Write("Choice: ");

                string izbor = Console.ReadLine()?.ToLower();
                if(izbor == "exit")
                {
                    Console.WriteLine("Exiting...");
                    break;
                }
            }

            if (iterationCount >= MAX_ITERATIONS)
            {
                Console.WriteLine("No more iterations available.\nApplication is closing...");
            }
        }
    }

}
