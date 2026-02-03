using System;
using Mscc.GenerativeAI;
using Microsoft.Extensions.Configuration;

namespace TranslationApp {

    class Program
    {

        const int MAX_ITERACIJA = 10;
        const int MAX_POKUSAJA = 3;
        const int BATCH_SIZE = 15;
        const int PAUZA = 2000;

        static async Task Main(string[] args)
        {

            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false).Build();

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
            Console.WriteLine($"You have a maximum of {MAX_ITERACIJA} iterations.\n");

            int brojIteracija = 0;

            while (brojIteracija < MAX_ITERACIJA)
            {
                brojIteracija++;

                Console.Clear();

                Console.WriteLine($"Iteration {brojIteracija}/{MAX_ITERACIJA}");
                Console.WriteLine("Enter terms (type 'OK' when finished):\n");

                List<string> termini = new List<string>();

                while (true)
                {
                    string unos = Console.ReadLine()?.Trim();

                    if (unos?.ToUpper() == "OK")
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(unos))
                    {
                        termini.Add(unos.Trim());
                    }
                    
                }
                if(termini.Count == 0)
                {
                    Console.WriteLine("No terms entered.");
                    continue;
                }

                for(int i = 0; i < termini.Count; i += BATCH_SIZE)
                {
                    List<string> batch = termini.Skip(i).Take(BATCH_SIZE).ToList();

                    int batchBroj = (i/BATCH_SIZE) + 1;

                    bool uspesno = false;
                    int brojPokusaja = 0;

                    while(!uspesno && brojPokusaja < MAX_POKUSAJA)
                    {
                        brojPokusaja++;
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
                            
                            uspesno = true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error. Attempt {brojPokusaja}/{MAX_POKUSAJA}.");
                            Console.WriteLine(e.Message);
                            if(brojPokusaja < MAX_POKUSAJA)
                            {
                                await Task.Delay(2000*brojPokusaja);
                            }
                        }
                    }
                    if (!uspesno)
                    {
                        Console.WriteLine($"Failed for batch number {batchBroj}");
                    }
                    if (i + BATCH_SIZE < termini.Count)
                    {
                        await Task.Delay(PAUZA);
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
            if (brojIteracija >= MAX_ITERACIJA) 
            {
                Console.WriteLine("No more iterations available.\nApplication is closing...");
            }
        }


    }

}
