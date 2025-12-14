using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TranslationApp;

string ollamaEndpoint = "http://127.0.0.1:11434";
HttpClient ollamaClient = new HttpClient
{
    BaseAddress = new Uri(ollamaEndpoint)
};//ova klasa se koristi za slanje HTTP zahteva
string modeName = await SelectOllamaModel(ollamaClient);

await StartChat(ollamaClient, modeName);
//Console.WriteLine("Exiting the application.");

static async Task<bool> ChatWithModel(HttpClient ollamaClient, ChatRequest chatRequest)
{
    // traži se unos od korisnika i čita se linija teksta
    Console.Write("user > ");

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
        Console.WriteLine("Failed to deserialize the response.");

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
                Console.WriteLine($"({i+1}) {model.Name}");
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
            return modelsResponse.Models[modelIndex-1].Name;
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
        Console.WriteLine($"Hello I am a friendly AI assistant, model {modelName}. How can I help you?");
        Console.WriteLine("To end the chat type /bye");
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
/*
namespace TranslationApp
{
    public class Program
    {

        private static readonly HttpClient _client = new HttpClient();
        private const string _ollamaApiUrl = "http://localhost:11434/api/generate";
        private const string _ollamaModel = "llama2";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Izaberite opciju:");
            Console.WriteLine("1 - Prevedi red po red");
            Console.WriteLine("2 - Prevedi ceo tekst odjednom");
            Console.WriteLine("Opcija: ");

            string option = Console.ReadLine();

            Console.WriteLine("\nUnesite tekst za prevod: ");
            string text = ReadMultipleLines();

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("Niste uneli tekst za prevod.");
                return;
            }

            string translatedText = "";

            switch (option)
            {
                case "1":
                    //translatedText = 
                    break;
                case "2":
                    break;
                default:
                    Console.WriteLine("Odaberite jednu od ponuđenih opcija!");
                    break;
            }


            var OllamaData = new OllamaGenerateRequest
            {
                Model = _ollamaModel,
                Prompt = $"Odgovaraj ISKLJUČIVO na SRPSKOM jeziku. Napiši detaljan, pun i jasan tekst o temi: {text} sa naslovom i tekstom u punim rečenicama, bez engleskih delova.",
                Stream = false
            };

            string json = JsonSerializer.Serialize(OllamaData);// saljem mu format koji ocekuje
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(_ollamaApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                OllamaGeneratedResponse? ollamaResponse = JsonSerializer.Deserialize<OllamaGeneratedResponse>(responseContent);

                //ollama vrati sve u stringu pa to parsiramo

                string fullGeneratedText = ollamaResponse?.Response?.Trim() ?? "Nije generisan tekst";

                string[] lines = fullGeneratedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string title = lines.Length > 0 ? lines[0] : "Nema nasova";
                string body = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1) : "Nema teksta";

                PostResult postResult = new PostResult { Title = title, Body = body, FullResponse = fullGeneratedText };

                Console.WriteLine("OLLAMA\n");
                Console.WriteLine($"Naslov: **{postResult.Title}**");
                Console.WriteLine($"Tekst: {postResult.Body}");
            }
            else
            {
                Console.WriteLine("Greska: " + response.StatusCode);
            }

            
        }

        public static string ReadMultipleLines()
        {
            StringBuilder sb = new StringBuilder();
            string line;

            while ((line = Console.ReadLine()) != null)
            {
                sb.AppendLine(line);
            }

            string result = sb.ToString();
            return result;
        }

        public static string TranslateLineByLine(string text)
        {

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
        }
    }

    public class OllamaGeneratedResponse
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
    }


    
}*/
