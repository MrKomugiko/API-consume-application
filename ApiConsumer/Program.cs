using ApiConsumer.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;
using System.Linq;

namespace ApiConsumer
{
    class Program
    {
        // Utworzenie klienta Http, który umożliwi komunikacje z zewnętrznycm API
        HttpClient client = new HttpClient();
        // Globalne wyniki wyszukiwania bo są wykorzystywane w całym projekcie
        Query wynikiWyszukiwania;
        //lista wyszukiwanych elementow
        static List<SearchingHistory> OldHistoryOfSearching = new List<SearchingHistory>();
        static List<SearchingHistory> CurrentHistoryOfSearching = new List<SearchingHistory>();
        static async Task Main(string[] args) {
            // Niekończąca się pętla
                await DisplayMenu();
        }

        #region StartMenu
        static async Task DisplayMenu() {
            Console.Clear();
            Console.WriteLine(" ********************************\n" +
                              " ........:: Wikipedia ::........\n " +
                              " ********************************\n" +
                              "\t[1] Wyszukiwarka.\n" +
                              "\t[2] Zapisz dzisiejszą sesje wyszukiwania\n" +
                              "\t[3] Wyswietl ranking popularnosci - ogolny/całkowity. \n" +
                              "\t[4] ...\n" +
                              "\t[5] Zamknij.\n");
            
            var answer = Console.ReadLine();
            switch (Convert.ToInt32(answer)) {
                case 1:
                    while (true) {
                        Console.Clear();
                        int page = 1;
                        // Pobranie od użytkownika wartości do wyszukania
                        string szukanaFraza;
                        do {
                            Console.Write("Wyszukaj: ");
                            szukanaFraza = Console.ReadLine();
                        } while (szukanaFraza == String.Empty);
                        // Utworzenie instancji klasy program  
                        Program program = new Program();
                        search:
                        // Asynchroniczne wywołanie - czekanie aż skończy się pobieranie danych 
                        await program.SearchInWikipedia(szukanaFraza, page - 1);
                        Console.WriteLine("Przejsc na następna strone? [t/n] lub podaj numer strony.\n[ENTER] aby przejść dalej.\n");
                        var answer2 = Console.ReadLine();
                        Console.Clear();
                        // Sprawdzanie wartosci wprowadzonych rpzez uzytkownika, w razie błedu wraca do początku.
                        try {
                            if (answer2 != "" && answer2 != "n") {
                                if (answer2.ToLower() == "t") {
                                    page++;
                                    goto search;
                                }
                                if ((Convert.ToInt32(answer2) > 1001) && (Convert.ToInt32(answer2) >= (program.wynikiWyszukiwania.searchinfo.totalhits / 10))) {
                                    Console.WriteLine($"Wprowadz poprawny numer strony [1 - {program.wynikiWyszukiwania.searchinfo.totalhits / 10} ]");
                                    goto restart;
                                }
                                if (Convert.ToInt32(answer2) >= 1) {
                                    page = Convert.ToInt32(answer2);
                                    goto search;
                                }
                            }
                        } catch (FormatException) {
                            Console.WriteLine($"Wprowadz poprawny numer strony [1 - {((program.wynikiWyszukiwania.searchinfo.totalhits / 10) < 1000 ? ((program.wynikiWyszukiwania.searchinfo.totalhits / 10)) : 1000)} ]");
                            goto restart;
                        }
                        int articleId = 1;
                        do {
                            Console.Write("Wybierz artykuł podając jego id [1-10], [0 Aby zakończyć] \n");
                            try {
                                articleId = Convert.ToInt32(Console.ReadLine());
                                Console.Clear();
                                await program.GetWikipediaArticleById(program.wynikiWyszukiwania.search[articleId - 1].pageid);
                                // Utworzenie wpisu do loga 
                                MakeSearchingLog(program.wynikiWyszukiwania.search[articleId - 1].pageid, szukanaFraza, program.wynikiWyszukiwania.search[articleId - 1].title);
                                break;

                            } catch (Exception e) {
                                Console.WriteLine("Wprowadz poprawny numer artykułu, aby zakończyć, wybierz [0].");
                                // Console.WriteLine("DEBUG:" + e);
                            }

                        } while (articleId != 0);

                        Console.WriteLine("\t[ENTER] aby kontynuowac wyszukiwanie.\n" +
                                          "\t[M] aby wrócić do menu. ");

                        answer = Console.ReadLine().ToLower();
                        if (answer == "m") await DisplayMenu();
                    }
                case 2:
                    await SavingAndLoadingHistory(CurrentHistoryOfSearching);
                    break;
                case 3:
                    if (OldHistoryOfSearching.Count != 0) { 
                        ShowRanking(GenerateRanking(OldHistoryOfSearching)); }
                    else {
                        await SavingAndLoadingHistory(CurrentHistoryOfSearching);
                        ShowRanking(GenerateRanking(OldHistoryOfSearching));
                    }

                    break;
                default:
                    await DisplayMenu();
                    break;
            }
            restart:
            Console.WriteLine("\t[M] aby wrócić do menu. ");
            try {
                answer = Console.ReadLine();
                if (answer.ToLower() == "m") await DisplayMenu();
            } catch (FormatException) {
                goto restart;
            }
        }
        #endregion

        #region Wikipedia API
        // Wyświetlenie wyników z Wikipedi.
        private async Task SearchInWikipedia(string searchQuery, int page) {
                // Console.WriteLine("Rozpoczęcie wyszukiwania...");
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
                // Console.WriteLine("Wyszukiwanie zakończone");
            }
            // Wyświetlenie Pierwszych lini tekstu w artykule
            private async Task GetWikipediaArticleById(int pageId, int length = 500) {
                // Console.WriteLine("Pobieranie artykułu...");
                // Pobranie odpowiedzi z API Wikipedii jako parametry przekazujemy wczesniej ustalony ID strony 
                //      oraz długość tekstu jaka ma zostać wyświetlona domyślnie 500 znakow.
                string response = await client.GetStringAsync(
                $"https://pl.wikipedia.org/w/api.php?action=query&prop=extracts&exchars={length}&pageids={pageId}&format=json&explaintext=1");
                // Zrzutowanie otrzymanej odpowiedzi na klase Json
                JObject o = JObject.Parse(response);
                // Wyciągnięcie opisu dla danego id strony
                JToken tekstArtykulu = o.SelectToken($"$.query.pages.{pageId}.extract");
                // Wyświetlenie artykułu.
                Console.WriteLine(tekstArtykulu.ToString());
            }
        #endregion

        #region Saving and loaging stuff
            private static void MakeSearchingLog(int pageId, string searchingQuery, string pageTitle) {
                SearchingHistory HistoryLog = new SearchingHistory {
                    DataWyszukiwania = DateTime.Now,
                    TytulWyszukanejStrony = pageTitle,
                    OdwiedzonaStrona = pageId,
                    WyszukiwanaFraza = searchingQuery
                };
                CurrentHistoryOfSearching.Add(HistoryLog);
            }
            private static async Task SavingAndLoadingHistory(List<SearchingHistory> currentHistory) {
            // Zaimportowanie aktualnie przechowywanej listy z pliku w przypadku gdy ta jest pusta      
            if (OldHistoryOfSearching.Count == 0) {
                    //////////File.WriteAllText("D:\\searchinghistory.txt", null);
                    var jsonFromFile = await File.ReadAllTextAsync("D:\\searchinghistory.txt");
                    if(jsonFromFile != null) {
                        OldHistoryOfSearching = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SearchingHistory>>(jsonFromFile.ToString());
                    } else {
                        OldHistoryOfSearching.Add(currentHistory.First());
                    }
                }

                // Aktualizowanie pliku na koniec działąnia programu pooprzez połączenie starej i nowej listy
                //   następnie jej zapisanie do pliku ? nie wiem dlaczego nie moge do nadpisać a sam sie kasuje
                //   więc nieefektywna opcja, pobieranie całości i zapisywanie całości od nowa 
                OldHistoryOfSearching.AddRange(currentHistory);
                // Po przepisaniu wynikow aktualna liczba wyszukiwan zostaje wyczyszczona
                //   w innym wypadku liczba elementow zapisywanych podczas dzialania aplikacji, by je namnażała
                currentHistory = null;
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(OldHistoryOfSearching);
                File.WriteAllText("D:\\searchinghistory.txt", jsonString);
            }
        #endregion
        
        #region Ranking stuff
            private static List<Ranking> GenerateRanking(List<SearchingHistory> currentHistory) {

                List<Ranking> ranking = new List<Ranking>();
                //pogrupowanie po jednakowych odwiedzonych stronach
                var groupedResult = currentHistory.GroupBy(p => p.OdwiedzonaStrona);

                int counter = 1;
                //sprawdzenie co jest w grupach
                foreach (var visitedGroup in groupedResult) {
                    counter++;
                    // Wypisanie unikatowych pageId ktore zostały odwiedzone
                    ranking.Add(new Ranking {
                        Id = visitedGroup.Key, // pageId
                        Position = counter, // pozycja w rankingu
                        SearchedByQueryList = new List<string>(), // lista wyszukiwan
                        Title = "", // tytuł
                        Visited = 0 // suma wystąpień strony
                        }
                    );

                foreach (SearchingHistory history in visitedGroup) {
                        // w pętli dodawane będą wszystkie możliwe zapytania wywołane aby uzystać dostęp do tej konkretnej strony
                        ranking.Where(p => p.Id == visitedGroup.Key).First().SearchedByQueryList.Add(history.WyszukiwanaFraza);
                        ranking.Where(p => p.Id == visitedGroup.Key).First().Visited = visitedGroup.Count();
                        ranking.Where(p => p.Id == visitedGroup.Key).First().Title = history.TytulWyszukanejStrony;
                };
                
                ranking = ranking.OrderByDescending(p => p.Visited).ToList();
                counter = 1;
                foreach (var pozycja in ranking) {
                    pozycja.Position = counter;
                    counter++;
                }

            }
            return ranking;
            }
            private static void ShowRanking(List<Ranking> rankingData) {                                                                                                                                     
                   foreach (var element in rankingData)                                                                     
                    {                                                                                                     
                    Console.WriteLine($"Pozycja[{element.Position}] | Tytul[{element.Title}] | Wyswietlenia[{element.Visited}]");                                                                                        
                }
            Console.WriteLine();
            }
        #endregion

        #region NOTATNIK / TODOs / UWAGI I POMYSŁY
        /*
         * [DONE] TODO: Zabezpieczenie wprowadzanych danych przed wywaleniem błedu xD = "idiotoodporna" aplikacja
         * [DONE] TODO: Ogarnięcie w jakikolwiek lepszy sposób wyświetlanie tekstu artykuów z pominięciem znaczników HTML,
         *              rozwiązanie => dodanie "explaintext=1" do url wikipedi
         * [DONE] TODO: Zmiana języka wyszukiwarki na polski d[-.o]b 
         * [DONE] TODO: Poprawienie jakości wyszukiwania => wyświetlane były już przesunięte pozycje 
         *              (bez tych najbardziej pasujących, tylko od następnych 10ciu)
         * [DONE] TODO: Zapisywanie historii przeglądania
         *        TODO: Refraktoryzacja kodu - żeby był troche bardziej czytelny ew. rozbicie na mniejsze metody
         * [----] TODO: Wyświetlanie losowego artykułu
         *
         *
         *
         * [DONE] TODO: Zapisywanie historii 
         * [DONE] TODO: Tworzenie rankingu
         * [DONE] TODO: Automatyczna aktualizacja pliku z historią wyszukiwania
         *        TODO: Plik historii eksportowany jest do pliku podczas kończenia programu
         * [DONE] TODO: wyświetlanie rankingu
         * [DONE] TODO: Wyświetlanie posortowanego rankingu
         * [DONE] TODO: Korekcja przyjmowanych wartości w klasie Ranking -> Title, zamiast wyświetlać tytuł z klasy "Search" 
         *              pokazuje OdwiedzonaStrona z klasy SearchHistory <- ta powinna posiadać oprócz tego tytuł,
         *              który będzie sobie przypisywac w czasie tworzenia
         * [DONE] TODO: Dodanie opcji wyświetlenia rankingu poprzez komende w konsoli na początku / końcu programu
         *        TODO: Wyodrebnienie Menu, zeby nie było trzeba przeszukiwać wikipedi zeby sprawdzic ranking/zapisac hisotie itp
         */
        #endregion
    }
}