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
                Console.WriteLine("Greska - api kljuc nije pronadjen.");
                return;
            }

            GoogleAI googleAI = new GoogleAI(api);
            GenerativeModel model = googleAI.GenerativeModel("models/gemini-1.5-flash");
            
            Console.WriteLine("PREVODILAC");
            Console.WriteLine("Unesite tekst na engleskom u odvojenim redovima. Kada zavrsite pritisnite unesite 'OK'.");
            Console.WriteLine($"Imate maksimalno {MAX_ITERACIJA} iteracija.\n");

            int brojIteracija = 0;

            while (brojIteracija < MAX_ITERACIJA)
            {
                brojIteracija++;

                Console.Clear();

                Console.WriteLine($"Iteracija broj {brojIteracija}/{MAX_ITERACIJA}");
                Console.WriteLine("Unesite termine (ukucajte 'OK' kada zavrsite):\n");

                List<string> termini = new List<string>();

                while (true)
                {
                    string unos = Console.ReadLine()?.Trim();

                    if (unos?.ToUpper() == "OK")
                    {
                        break;
                    }

                    //ignorise prazne redove
                    if (!string.IsNullOrWhiteSpace(unos))
                    {
                        termini.Add(unos.Trim());
                    }
                    
                }
                if(termini.Count == 0)
                {
                    Console.WriteLine("Nema unetih termina.");
                    continue;// prekidam while petlju korisnik gubi jednu iteraciju
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
                            string recnik = @"
                                            KORISTI SLEDECE PREVODE ZA KONZISTENTNOST:
                                            - Battery -> Baterija
                                            - No-load Speed -> Broj obrtaja u praznom hodu
                                            - Max. Torque -> Maksimalni obrtni moment
                                            - Square Drive Size -> Veličina četvrtke
                                            - Standard Bolt Size -> Veličina standardnog vijka
                                            - Net Weight -> Neto težina
                                            - Triple Function -> Trostruka funkcija
                                            - Variable Speed -> Varijabilna brzina
                                            - LED Light -> LED svetlo
                                            - Battery Indicator -> Indikator napunjenosti baterije
                                            - Over-temperature Protection -> Zaštita od pregrevanja
                                            - Overcharge Protection -> Zaštita od prepunjavanja
                                            - Over Discharge Protection -> Zaštita od prekomernog pražnjenja
                                            - Overcurrent Protection -> Zaštita od prekomerne struje
                                            - Brake -> Kočnica
                                            - Spindle Lock -> Blokada vretena
                                            - Impact Function -> Vibraciona funkcija
                                            - Plastic Case Packing -> Plastični kofer (pakovanje)
                                            - Tool Free Flange -> Prirubnica za brzu izmenu
                                            - Wrench -> Ključ
                                            - Scraper -> Strugač
                                            - Belt Clip -> Zakačka za kaiš (kopča)
                                            - Socket -> Nasadni ključ (gedora)
                                            - 4A Battery Charger*1 -> Punjač baterija (4A) 1 kom
                                            - 5.0Ah Battery Pack *2pcs -> Baterijsko pakovanje (5.0Ah) 2 kom
                                            - Plastic Case Packing -> Plastični kofer (pakovanje)
                                            - Color Box Packing -> Pakovanje u kutiji u boji
                                            - Auxiliary Handle -> Pomoćna ručka
                                            - < Test Date > -> < Datum testiranja >
                                            ";
                            string prompt = $@"
                                            Ti si stručni prevodilac za električne alate i mašine. 
                                            {recnik}
                                            
                                            ZADATAK:
                                            Prevedi sledeće tehničke termine sa engleskog na srpski jezik koristeći gore navedenu terminologiju gde je primenljivo.
                                            Odgovori ISKLJUČIVO u formatu: 'svi engleski termini' pa jedan prazan red, pa 'svi srpski prevodi'. Svaki termin mora biti u zasebnom redu.
                                            Ukoliko su termini na engleskom numerisani zadrzi ISTU numeraciju i na srpskom. Ukoliko se nalaze tačka ili zvezdica ili bilo kakvo slično obeležje na početku reda, njih ZANEMARI.
                                            
                                            TERMINI ZA PREVOD:
                                            {string.Join("\n", batch)}";
                            Console.WriteLine("Generisanje prevoda u toku...");
                            var response = await model.GenerateContent(prompt);
                            Console.WriteLine("Odgovor primljen!");

                            Console.WriteLine(response.Text);
                            
                            uspesno = true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Greska. Pokusaj broj {brojPokusaja}/{MAX_POKUSAJA}.");
                            Console.WriteLine(e.Message);
                            if(brojPokusaja < MAX_POKUSAJA)
                            {
                                await Task.Delay(2000*brojPokusaja);
                            }
                        }
                    }
                    if (!uspesno)
                    {
                        Console.WriteLine($"Neuspeh za batch broj {batchBroj}");
                    }
                    if (i + BATCH_SIZE < termini.Count)
                    {
                        await Task.Delay(PAUZA);
                    }
                }
                Console.WriteLine("Prevodjenje zavrseno.\nOpcije:");
                Console.WriteLine("ENTER - novi prevod");
                Console.WriteLine("'kraj' - izlaz");
                Console.Write("Izbor: ");

                string izbor = Console.ReadLine()?.ToLower();
                if(izbor == "kraj")
                {
                    Console.WriteLine("Izlazak...");
                    break;
                }

            }
            if (brojIteracija >= MAX_ITERACIJA) 
            {
                Console.WriteLine("Nemate vise iteracija.\nAplikacija se zatvara...");
            }
        }


    }

}