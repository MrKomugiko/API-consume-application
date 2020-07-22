using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ApiConsumer
{
    class Program
    {
        // Utworzenie klienta Http, który umożliwi komunikacje z zewnętrznycm API
        HttpClient client = new HttpClient();
        // Globalne wyniki wyszukiwania bo są wykorzystywane w całym projekcie
        Query wynikiWyszukiwania;
        static async Task Main(string[] args) {
            // Niekończąca się pętla
            while (true) {
                int page = 1;
                // Pobranie od użytkownika wartości do wyszukania
                string szukanaFraza;
                do{
                    Console.Clear();
                    Console.WriteLine(  "********************************\n"+
                                        "........:: Wikipedia ::........\n"+
                                        "********************************");
                    Console.Write("Wyszukaj: ");
                    szukanaFraza = Console.ReadLine();
                }while(szukanaFraza == String.Empty);
                // Utworzenie instancji klasy program  
                Program program = new Program();
                search:
                // Asynchroniczne wywołanie - czekanie aż skończy się pobieranie danych 
                await program.SearchInWikipedia(szukanaFraza, page-1);
                restart:
                Console.WriteLine("Przejsc na następna strone? [t/n] lub podaj numer strony.\n[ENTER] aby przejść dalej.\n");
                var answer = Console.ReadLine();
                // Sprawdzanie wartosci wprowadzonych rpzez uzytkownika, w razie błedu wraca do początku.
                try {
                    if (answer != "" && answer!="n") {
                        if (answer.ToLower() == "t") {
                            page++;
                            goto search;
                        }
                        if ((Convert.ToInt32(answer) > 1001) && (Convert.ToInt32(answer) >= ( program.wynikiWyszukiwania.searchinfo.totalhits / 10))) {
                            Console.WriteLine($"Wprowadz poprawny numer strony [1 - {program.wynikiWyszukiwania.searchinfo.totalhits / 10} ]");
                            goto restart;
                        }
                        if (Convert.ToInt32(answer) >= 1) {
                            page = Convert.ToInt32(answer);
                            goto search;
                        }
                    }
                } catch (FormatException) {
                        Console.WriteLine($"Wprowadz poprawny numer strony [1 - {((program.wynikiWyszukiwania.searchinfo.totalhits/10)<1000?((program.wynikiWyszukiwania.searchinfo.totalhits / 10)):1000)} ]");
                        goto restart;
                    }
                int articleId=1;
                do {
                    Console.Write("Wybierz artykuł podając jego id [1-10], [0 Aby zakończyć] \n");
                        try {
                            articleId = Convert.ToInt32(Console.ReadLine());
                            await program.GetWikipediaArticleById(program.wynikiWyszukiwania.search[articleId - 1].pageid);
                            break;

                        } catch (Exception e) {
                            Console.WriteLine("Wprowadz poprawny numer artykułu, aby zakończyć, wybierz [0].");
                           // Console.WriteLine("DEBUG:" + e);
                        }
                  
                } while (articleId !=0) ;

                Console.WriteLine("[ENTER] aby kontynuowac.");
                Console.Read();
            }
        }

        // Wyświetlenie wyników z Wikipedi.
        private async Task SearchInWikipedia(string searchQuery, int page) {
            Console.WriteLine("Rozpoczęcie wyszukiwania...");
            string response = await client.GetStringAsync(
                $"https://pl.wikipedia.org/w/api.php?action=query&format=json&list=search&srsearch={searchQuery}&sroffset={page * 10}");
            // Przekonwertowanie otrzymanych wyników za pomocą metody klasy JsonConverter, 
            //      ten mapuje otrzymane wyniki z formatu Json do obiektu klasy który został stworzony
            //      klasa "Respond" posiada odwołanie do klasy "Query" w któej znajduje sie lista "Search" z pobranymi danymi.
            Respond search = JsonConvert.DeserializeObject<Respond>(response);
            // W przypadku wyszukiwania kolejnych stron, konieczne jest ustawienie przesunięcia wyników o 10 
            Continue newPage = new Continue();
            newPage.sroffset = (page)*10;
            search._continue = newPage;
            // Przypisanie zwróconych danych do nowej listy ( dla celów estetycznych, łątwiejszego użycie pożniej w kodzie)
            wynikiWyszukiwania = search.query;
            // Wyświetlenie listy tytułów artykułów znalezionych w Wikipedii.
            int index = 1;
            foreach (Search item in wynikiWyszukiwania.search) {
                Console.WriteLine($"[{index++}] {item.title}");
            }
            Console.WriteLine($"Strona [{(search._continue.sroffset / 10)+1} / {((search.query.searchinfo.totalhits / 10)<1000? (search.query.searchinfo.totalhits / 10):1000)}]");
            Console.WriteLine("Wyszukiwanie zakończone");
        }

        // Wyświetlenie Pierwszych lini tekstu w artykule
        private async Task GetWikipediaArticleById(int pageId, int length = 500) {
            Console.WriteLine("Pobieranie artykułu...");
            // Pobranie odpowiedzi z API Wikipedii jako parametry przekazujemy wczesniej ustalony ID strony 
            //      oraz długość tekstu jaka ma zostać wyświetlona domyślnie 500 znakow.
            string response = await client.GetStringAsync(
            $"https://pl.wikipedia.org/w/api.php?action=query&prop=extracts&exchars={length}&pageids={pageId}&format=json&explaintext=1");

            // Zrzutowanie otrzymanej odpowiedzi na klase Json
            JObject o = JObject.Parse(response);
            // Wyciągnięcie opisu dla danego id strony
            JToken acme = o.SelectToken($"$.query.pages.{pageId}.extract");
            // Wyciągnięcie drugiego paragrafu <p> z tekstu w formacie HTML
            //      poprzez ręczne przycięcie tekstu szukając znacznika <p>
            string artykul = acme.ToString();
            // Usuwanie niepotrzebnych znaczników, w aplikacji konsolowej i tak sie nie przydadzą ;d
     
            artykul = artykul.Replace("<b>", "").Replace("</b>", "").Replace("<p>", "").Replace("</p>", "");
            // Wyświetlenie artykułu.
            Console.WriteLine(artykul);
        }

        /*
         * [DONE] TODO: Zabezpieczenie wprowadzanych danych przed wywaleniem błedu xD = "idiotoodporna" aplikacja
         * [DONE] TODO: Ogarnięcie w jakikolwiek lepszy sposób wyświetlanie tekstu artykuów z pominięciem znaczników HTML,
         *              rozwiązanie => dodanie "explaintext=1" do url wikipedi
         * [DONE] TODO: Zmiana języka wyszukiwarki na polski d[-.o]b 
         * [DONE] TODO: Poprawienie jakości wyszukiwania => wyświetlane były już przesunięte pozycje 
         *              (bez tych najbardziej pasujących, tylko od następnych 10ciu)
         * TODO: Zapisywanie historii przeglądania
         * TODO: Refraktoryzacja kodu - żeby był troche bardziej czytelny ew. rozbicie na mniejsze metody
         * TODO: Wyświetlanie losowego artykułu
         */
    }
}