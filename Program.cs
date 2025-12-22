using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TranslationApp;
using static System.Net.Mime.MediaTypeNames;

namespace TranslationApp {

    class Program
    {
        static string dictionary;
        static async Task Main(string[] args)
        {
            dictionary = File.ReadAllText("data/dictionary.json");
            string ollamaEndpoint = "http://127.0.0.1:11434";
            HttpClient ollamaClient = new HttpClient
            {
                BaseAddress = new Uri(ollamaEndpoint)
            };//ova klasa se koristi za slanje HTTP zahteva
            string selectedModel = await SelectOllamaModel(ollamaClient);

            if (string.IsNullOrEmpty(selectedModel))
            {
                Console.WriteLine("Greška: Model nije izabran!");
                return;
            }

            Console.Clear();
            Console.WriteLine($"Model: {selectedModel}\n");
            string translatedText = "";

            ChatRequest chatRequest = new ChatRequest
            {
                Model = selectedModel,
                Messages = [], // MNOGO VAŽNO - inicijalizuje se prazna lista poruka
                               // Ollama chat API zahteva listu celog konteksta razgovora
                Stream = false // znači da ne želimo streaming odgovora
                               // odgovor dolazi odjednom, a ne deo po deo
            };

            Message systemMessage = new Message
            {
                Role = "system",
                Content = $@"You are a strict technical translator for machine manuals. 
                     Your task is to translate input text from English to Serbian. 
                     STRICT RULES: 
                     1. Output ONLY the translated text in Serbian. 
                     2. Do NOT provide explanations, introductions, or notes. 
                     3. Do NOT reply in English under any circumstances. 
                     4. Do NOT say 'Here is the translation' or similar fillers. 
                     5. For technical terminology translation:
                     - Use ONLY the dictionary provided for specific technical terms
                     - Translate all other words (grammar, common words, context) normally in Serbian
                     - Example: If dictionary has 'drill: busilica' and input is 'drills are used for making holes', 
                      translate as 'busilice se koriste za pravljenje/busenje rupa' 
                      (use 'busilice' from dictionary, translate rest yourself)
                 
                     Dictionary for technical terms: {dictionary}"
            };
            chatRequest.Messages.Add(systemMessage);

            do
            {
                Console.WriteLine("\nIzaberite opciju prevođenja:");
                Console.WriteLine("1 - Red po red");
                Console.WriteLine("2 - Batch");
                Console.WriteLine("3 - Ceo tekst");
                Console.WriteLine("stop - Izlazak");
                Console.WriteLine("\nOpcija: ");

                string option = Console.ReadLine();

                switch (option)
                {
                    case "1":
                        do
                        {
                            translatedText = await TranslateLineByLine(ollamaClient, selectedModel, chatRequest);
                        } while (translatedText != "stop" && translatedText != "back");
                        break;
                    case "2":
                        do
                        {
                            translatedText = await TranslateInBatches(ollamaClient, selectedModel, chatRequest);
                        } while (translatedText != "stop" && translatedText != "back");
                        break;
                    case "3":
                        translatedText = await TranslateEntireText(ollamaClient, selectedModel, chatRequest);
                        break;
                    case "stop":
                        translatedText = "stop";
                        break;
                    default:
                        Console.WriteLine("Odaberite jednu od ponuđenih opcija!");
                        break;
                }

                if(translatedText == "stop")
                {
                    Console.WriteLine("Da li ste sigurni da zelite da izadjete? (d/n)");
                    string decision = Console.ReadLine();
                    if(decision == "n")
                    {
                        translatedText = "";
                    }

                }
            }
            while (translatedText != "stop");


            // cuvanje u fajl


            Console.WriteLine("Da li želite da uđete u chat mood za dodatne izmene? (d/n)");
            string response = Console.ReadLine()?.ToLower();

            if (response == "d")
            {
                await StartChat(ollamaClient, selectedModel, chatRequest); // USLA SAM U DOP !!!!!!!!!!!!!!!!
            }
            else
            {
                Console.WriteLine("Doviđenja!");
            }


            Console.WriteLine("\nPritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
            
        }

        static async Task<bool> ChatWithModel(HttpClient ollamaClient, ChatRequest chatRequest)
        {
            // traži se unos od korisnika i čita se linija teksta
            Console.Write("korisnik > ");

            Boolean continueConversation = false;
            string userInput = Console.ReadLine();

            // ako korisnik unese /bye ili samo pritisne Enter (prazan unos), vraća se FALSE
            // što prekida while petlju u funkciji StartChat
            if (userInput == "/bye" || string.IsNullOrWhiteSpace(userInput))
            {
                return continueConversation = false;
            }

            // poziva se funkcija ChatCompletion za slanje zahteva modelu
            ChatResponse? chatResponse = await ChatCompletion(ollamaClient, chatRequest, userInput);

            // ako je dobijen odgovor
            if (chatResponse != null)
            {
                // kreira se nova poruka od asistenta (modela) iz dobijenog chatResponse-a
                Message assistantMessage = new Message { Role = chatResponse.Message.Role, Content = chatResponse.Message.Content };
                // VAŽNO - poruka modela se dodaje u listu chatRequest.Messages
                // time se poruka modela uključuje u kontekst za sledeći zahtev
                // MODEL PAMTI
                chatRequest.Messages.Add(assistantMessage);
                // ispisuje se odgovor modela
                Console.WriteLine($"{assistantMessage.Role} > {assistantMessage.Content}");

                // postavlja se na true, čime se signalizira da se chat nastavlja
                continueConversation = true;
            }
            else
            {
                // ako je odgovor null (npr greška pri deserijalizaciji),
                // ispisuje se greška i vraća se false, što prekida chat
                Console.WriteLine("Greška pri deserijalizaciji odgovora.");

                continueConversation = false;
            }

            return continueConversation;
        }

        // komunicira sa Ollama API-jem da bi dobila listu dostupnih modela
        // i omogućila korisniku da izabere jedan
        static async Task<string> SelectOllamaModel(HttpClient ollamaClient)
        {
            HttpResponseMessage responseMessage = await ollamaClient.GetAsync("/api/tags");// šaljem GET
            string content = await responseMessage.Content.ReadAsStringAsync(); // čitam odg

            if (responseMessage != null && responseMessage.StatusCode == HttpStatusCode.OK && content != null)
            {
                // pretvaranje (deserijalizacija) JSON stringa content u objekat klase ModelsResponse
                ModelsResponse modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(content)!;

                if (modelsResponse != null)
                {
                    // prolazim kroz listu modela i ispisujem svaki model sa rednim brojem
                    for (int i = 0; i < modelsResponse.Models.Count; i++)
                    {
                        Model? model = modelsResponse.Models[i];
                        Console.WriteLine($"({i + 1}) {model.Name}");
                    }

                    Console.WriteLine("\nPlease use the numeric value for the model to interact with.");

                    string userInput = Console.ReadLine();

                    // pokušava se parsiranje unosa u ceo broj (int.TryParse)
                    // ako parsiranje ne uspe, ili ako je broj van opsega dostupnih indeksa modela,
                    // ispisuje se greška i vraća se prazan string
                    if (!int.TryParse(userInput, out int modelIndex) || modelIndex < 1 || modelIndex > modelsResponse.Models.Count)
                    {
                        Console.WriteLine("Invalid model index.");

                        return string.Empty;
                    }
                    // ako je unos validan, vraća se ime izabranog modela
                    return modelsResponse.Models[modelIndex - 1].Name;
                }
            }
            // ako je došlo do greške vraća prazan string
            return string.Empty;
        }

        // postavlja inicijalne parametre i pokreće glavnu petlju za razgovor
        static async Task StartChat(HttpClient ollamaClient, string modelName, ChatRequest chatRequest)
        {
            // proverava da li je uspešno izabrano ime modela u prethodnom koraku
            if (modelName != string.Empty)
            {

                Console.WriteLine($"\nZdravo, ja sam aplikacija za prevođenje, model {modelName}. Kako mogu da ti pomognem?");
                Console.WriteLine("Da završiš dopisivanje ukucaj /bye");
                Console.WriteLine();

                // ova petlja se izvršava sve dok funkcija ChatWithModel vrća true
                // svaka iteracija petlje predstavlja jedan krug komunikacije
                while (await ChatWithModel(ollamaClient, chatRequest)) ;
            }
        }

        // funkcija komunicira direktno sa Ollama chat API-jem
        static async Task<ChatResponse?> ChatCompletion(HttpClient ollamaClient, ChatRequest chatRequest, string userInput)
        {
            // kreira se nova poruka sa ulogom user i tekstom koji je korisnik uneo
            Message userMessage = new Message { Role = "user", Content = userInput };

            // korisnikova poruka se dodaje u kontekst
            chatRequest.Messages.Add(userMessage);

            // 1. serijalizacija i priprema za slanje
            // kompletni chatRequest objekat (koji sadrži model i ceo kontekst razgovora)
            // se pretvara u JSON string
            string chatRequestJson = JsonSerializer.Serialize(chatRequest);
            // kreira se StringContent objekat koji sadrži JSON string
            // ovo je telo HTTP POST zahteva, i specificira se da je tip sadržaja application/json
            StringContent content = new StringContent(chatRequestJson, Encoding.UTF8, "application/json");

            // 2. slanje zahteva
            // šalje se POST zahtev na putanju /api/chat (standardna Ollama chat API putanja)
            // sa pripremljenim JSON sadržajem
            HttpResponseMessage responseMessage = await ollamaClient.PostAsync("/api/chat", content);

            // 3. obrada odgovora
            // čita se JSON odgovor iz modela
            string llmResponse = await responseMessage.Content.ReadAsStringAsync();
            // JSON odgovor se deserijalizuje u C# objekat ChatResponse
            ChatResponse? chatResponse = JsonSerializer.Deserialize<ChatResponse>(llmResponse);

            // vraća se objekat koji sadrži odgovor modela
            return chatResponse;
        }

        static async Task<string> TranslateLineByLine(HttpClient ollamaClient, string selectedModel, ChatRequest chatRequest)
        {
            Console.WriteLine("\nUnesite red za prevod: ");
            string line = Console.ReadLine();
            
            if(line == "stop")
            {
                return line;
            }
            if (line == "back")
            {
                return line;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return "Niste uneli tekst za prevod.";
            }

            Console.WriteLine("\nPrevođenje u toku...");

            ChatResponse? chatResponse = await ChatCompletion(ollamaClient, chatRequest, line);

            if (chatResponse != null)
            {
                Message assistantMessage = new Message { Role = chatResponse.Message.Role, Content = chatResponse.Message.Content };
                chatRequest.Messages.Add(assistantMessage);
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{assistantMessage.Role} > {assistantMessage.Content}");

            }
            else
            {
                Console.WriteLine("Greška pri deserijalizaciji odgovora.");

                return "";
            }

            return "";

        }

        static async Task<string> TranslateInBatches(HttpClient ollamaClient, string selectedModel, ChatRequest chatRequest)
        {
            Console.WriteLine("\nUnesite broj redova za prevod: ");
            string num = Console.ReadLine();
            if (num == "stop")
            {
                return num;
            }
            if (num == "back")
            {
                return num;
            }
            if (!int.TryParse(num, out int number) || number <= 0)
            {
                return "Morate uneti pozitivan broj, 'back' ili 'stop'.";
            }

            StringBuilder sb = new StringBuilder();

            Console.WriteLine($"Unesite {number} redova, 'back' za povratak na glavni meni ili 'stop' za prekid unosa:");

            for (int i = 0; i < number; i++)
            {
                string line = Console.ReadLine();

                if (line == "stop")
                    return line;
                if (line == "back")
                    return line;

                sb.AppendLine(line);
            }

            Console.WriteLine("\nPrevođenje u toku...");

            string linesToTranslate = sb.ToString();

            ChatResponse? chatResponse = await ChatCompletion(ollamaClient, chatRequest, linesToTranslate);

            if (chatResponse != null)
            {
                Message assistantMessage = new Message { Role = chatResponse.Message.Role, Content = chatResponse.Message.Content };
                chatRequest.Messages.Add(assistantMessage);
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{assistantMessage.Role} > {assistantMessage.Content}");

            }
            else
            {
                Console.WriteLine("Greška pri deserijalizaciji odgovora.");
            }

            return "";
        }

        static async Task<string> TranslateEntireText(HttpClient ollamaClient, string selectedModel, ChatRequest chatRequest)
        {
            Console.WriteLine("\nUnesite tekst za prevod: ");
            string txt = Console.ReadLine();

            if (txt == "stop")
            {
                return txt;
            }
            if (txt == "back")
            {
                return txt;
            }

            if (string.IsNullOrWhiteSpace(txt))
            {
                return "Niste uneli tekst za prevod.";
            }

            Console.WriteLine("\nPrevođenje u toku...");

            ChatResponse? chatResponse = await ChatCompletion(ollamaClient, chatRequest, txt);

            if (chatResponse != null)
            {
                Message assistantMessage = new Message { Role = chatResponse.Message.Role, Content = chatResponse.Message.Content };
                chatRequest.Messages.Add(assistantMessage);
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{assistantMessage.Role} > {assistantMessage.Content}");

            }
            else
            {
                Console.WriteLine("Greška pri deserijalizaciji odgovora.");

                return "";
            }

            return "";
        }

    }

}