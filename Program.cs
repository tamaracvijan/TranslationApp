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
        const string DEFAULT_MODEL = "gemma3:4b";
        const bool AUTO_SELECT_MODEL = false;
        static async Task Main(string[] args)
        {

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

            Console.WriteLine("Izaberite opciju prevođenja:");
            Console.WriteLine("1 - Red po red");
            Console.WriteLine("2 - Batch");
            Console.WriteLine("3 - Ceo tekst");
            Console.WriteLine("Opcija: ");

            string option = Console.ReadLine();
            string translatedText = "";

            switch (option)
            {
                case "1":
                    ChatRequest chatRequest = new ChatRequest
                    {
                        Model = selectedModel,
                        Messages = [], // MNOGO VAŽNO - inicijalizuje se prazna lista poruka
                                       // Ollama chat API zahteva listu celog konteksta razgovora
                        Stream = false // znači da ne želimo streaming odgovora
                                       // odgovor dolazi odjednom, a ne deo po deo
                    };
                    translatedText = await TranslateLineByLine(ollamaClient, selectedModel, chatRequest);
                    break;
                case "2":
                    translatedText = await TranslateInBatches(ollamaClient, selectedModel, 5);
                    break;
                case "3":
                    translatedText = await TranslateEntireText(ollamaClient, selectedModel);
                    break;
                default:
                    Console.WriteLine("Odaberite jednu od ponuđenih opcija!");
                    return;
            }

            Console.WriteLine("PREVOD:\n");
            Console.WriteLine(translatedText);

            // cuvanje u fajl

            Console.WriteLine("Da li želite da uđete u chat mood za dodatne izmene? (d/n)");
            string response = Console.ReadLine()?.ToLower();

            if (response == "d")
            {
                await StartChat(ollamaClient, selectedModel); // USLA SAM U DOP !!!!!!!!!!!!!!!!
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
        static async Task StartChat(HttpClient ollamaClient, string modelName)
        {
            // proverava da li je uspešno izabrano ime modela u prethodnom koraku
            if (modelName != string.Empty)
            {
                // brisanje celog sadržaja koji je trenutno prikazan u konzolnom prozoru
                Console.Clear();
                Console.WriteLine($"Zdravo, ja sam aplikacija za prevođenje, model {modelName}. Kako mogu da ti pomognem?");
                Console.WriteLine("Da završiš dopisivanje ukucaj /bye");
                Console.WriteLine();

                // kreira se novi objekat ChatRequest koji se koristi za slanje zahteva Ollama API-ju
                ChatRequest chatRequest = new ChatRequest
                {
                    Model = modelName,
                    Messages = [], // MNOGO VAŽNO - inicijalizuje se prazna lista poruka
                                   // Ollama chat API zahteva listu celog konteksta razgovora
                    Stream = false // znači da ne želimo streaming odgovora
                                   // odgovor dolazi odjednom, a ne deo po deo
                };
                // definisanje ponašanja modela
                Message userMessage = new Message { Role = "system", Content = "You are a helpfull assistant." };

                chatRequest.Messages.Add(userMessage);

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

        static string ReadMultipleLines()
        {
            StringBuilder sb = new StringBuilder();
            string line;
            int emptyLineCount = 0;

            while (true)
            {
                line = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                {
                    emptyLineCount++;
                    if (emptyLineCount >= 2)
                    {
                        break;
                    }
                }
                else
                {
                    emptyLineCount = 0;
                    sb.AppendLine(line);
                }
            }

            string result = sb.ToString();
            return result;
        }

        static async Task<string> TranslateLineByLine(HttpClient ollamaClient, string selectedModel, ChatRequest chatRequest)
        {
            Console.WriteLine("\nUnesite red za prevod: ");
            string line = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine("Niste uneli tekst za prevod.");
                return "";
            }

            Message userMessage = new Message { Role = "system", Content = $"You are a helpfull assistant who translates text from English to Serbian for machine manuals. Here is the text to translate {line}." }; chatRequest.Messages.Add(userMessage);
            chatRequest.Messages.Add(userMessage);

            ChatResponse? chatResponse = await ChatCompletion(ollamaClient, chatRequest, line);

            if (chatResponse != null)
            {
                Message assistantMessage = new Message { Role = chatResponse.Message.Role, Content = chatResponse.Message.Content };
                chatRequest.Messages.Add(assistantMessage);
                Console.WriteLine($"{assistantMessage.Role} > {assistantMessage.Content}");

            }
            else
            {
                Console.WriteLine("Greška pri deserijalizaciji odgovora.");

                return "";
            }

            return "";

        }

        static async Task<string> TranslateInBatches(HttpClient ollamaClient, string selectedModel, int v)
        {
            //Console.WriteLine("\nUnesite red za prevod: ");
            //string line = ReadMultipleLines();
            //
            //if (string.IsNullOrWhiteSpace(text))
            //{
            //    Console.WriteLine("Niste uneli tekst za prevod.");
            //    return;
            //}
            return "";
        }

        static async Task<string> TranslateEntireText(HttpClient ollamaClient, string selectedModel)
        {
            return "";
        }

    }

}
    

    /*public class OllamaGeneratedResponse
    {
        [JsonPropertyName("response")] // eksplicitno mapiranje
        public string Response { get; set; }
    }

    public class OllamaGenerateRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
        public bool Stream { get; set; } = false;
    }

    public class PostResult
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string FullResponse { get; set; }
    }*/