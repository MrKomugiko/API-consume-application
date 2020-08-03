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
using System.Text.RegularExpressions;

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
        static bool isSearchingListEmpty;
        //przechowywany ostatni artykul i jego tytuł
        static RecentReadedArticle RecentArticle;
        static async Task Main(string[] args) {
            // Niekończąca się pętla
                await DisplayMenu();
        }

        #region StartMenu ----------------------------------------------------------------------------------------------------------------------------------------------------------
        static async Task DisplayMenu() {
            Console.Clear();
            Console.WriteLine("*******************************************************\n" +
                              "\t.............:: Wikipedia ::.............\n" +
                              "*******************************************************\n" +
                              "\t[1] Wyszukiwarka.\n" +
                              "\t[2] Zapisz dzisiejszą sesje wyszukiwania\n" +
                              "\t[3] Wyswietl ranking popularnosci \n" +
                              "\t[4] [NEW] Word statistics - counter\n" +
                              "\t[5] Zamknij.\n");
            
            var answer = Console.ReadLine();
            switch (Convert.ToInt32(answer)) {
                case 1:
                    /// TUTAJ
                    await SearcHEngine();
                    break;
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
                case 4:
                    if (RecentArticle != null) {
                       await DisplayStatisticMenu();

                    } else {
                        Console.WriteLine("Odwiedź najpierw artykuł.");
                    }
                    break;
                case 5:
                    Environment.Exit(0);
                    break;
                default:
                    await DisplayMenu();
                    break;
            }
            restart:
            Console.WriteLine("\t[M] aby wrócić do menu. ");
            try {
                answer = Console.ReadLine();
                if (answer.ToLower() == "m" || answer=="") await DisplayMenu();
            } catch (FormatException) {
                goto restart;
            }

        }

        static async Task SearcHEngine() {
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
                if (!isSearchingListEmpty) {
                    back:
                    Console.WriteLine("Przejsc na następna strone? [t/n] lub podaj numer strony.\n[ENTER] aby przejść dalej.\n");
                    var answer2 = Console.ReadLine();
                    // Sprawdzanie wartosci wprowadzonych rpzez uzytkownika, w razie błedu wraca do początku.
                    try {
                        if (answer2 != "" && answer2 != "n") {
                            if (answer2.ToLower() == "t") {
                                page++;
                                goto search;
                            }
                            if ((Convert.ToInt32(answer2) > 1001) && (Convert.ToInt32(answer2) >= (program.wynikiWyszukiwania.searchinfo.totalhits / 10))) {
                                Console.WriteLine($"Wprowadz poprawny numer strony [1 - {program.wynikiWyszukiwania.searchinfo.totalhits / 10} ]");
                                goto back;
                            }
                            if (Convert.ToInt32(answer2) >= 1) {
                                page = Convert.ToInt32(answer2);
                                goto search;
                            }
                        }
                    } catch (FormatException) {
                        Console.WriteLine($"Wprowadz poprawny numer strony [1 - {((program.wynikiWyszukiwania.searchinfo.totalhits / 10) < 1000 ? ((program.wynikiWyszukiwania.searchinfo.totalhits / 10)) : 1000)} ]");
                        goto back;
                    }
                    int articleId = 1;
                    do {
                        // TODO: BUG -> wywala outofrange przy wyborze 0, ( nie za kazdym razem ?)
                        Console.Write("Wybierz artykuł podając jego id [1-10], [0] Aby zakończyć \n");
                        try {
                            articleId = Convert.ToInt32(Console.ReadLine());
                            await program.GetWikipediaArticleById(program.wynikiWyszukiwania.search[articleId - 1].pageid);
                            // Utworzenie wpisu do loga 
                            MakeSearchingLog(program.wynikiWyszukiwania.search[articleId - 1].pageid, szukanaFraza, program.wynikiWyszukiwania.search[articleId - 1].title);
                            break;
                        } catch (FormatException) {
                            Console.WriteLine("Wprowadziles niepoprawny numer artykulu.");
                        }
                    } while (articleId != 0);
                } else {
                    Console.WriteLine("Brak wyników :(");
                }

                Console.WriteLine("\t[ENTER] aby kontynuowac wyszukiwanie.\n" +
                                  "\t[M] aby wrócić do menu. ");

                var answer = Console.ReadLine().ToLower();
                if (answer == "m") await DisplayMenu();
            }
        }
        #endregion

        #region StatisticMenu ------------------------------------------------------------------------------------------------------------------------------------------------------
        static async Task DisplayStatisticMenu() {
            Console.Clear();
            Console.WriteLine($"Ostatnio przeglądany artykuł o na temat \"{RecentArticle.Title}\"\n");
            Console.WriteLine("*******************************************************\n" +
                             "\t.............:: STATISTICS ::............. \n" +
                             "******************************************************* \n" +
                             "\t[1] Oblicz liczbe znakow. \n" +
                             "\t[2] Oblicz liczbe słów. \n" +
                             "\t[3] Wskaż najpopularniejsze słowo. \n" +
                             "\t[4] [W.I.P] Oblicz procentowy udział znaków w artykule. \n" +
                             "\t[5] Wróć na stronę główną. \n" +
                             "\t[6] Przejdz do wyszukiwarki. \n"+
                             "\t[7] Zamknij program. \n");

            var answer = Console.ReadLine();
            switch (Convert.ToInt32(answer)) {
                case 1:
                    Console.WriteLine($"W wybranym artykule (fragmencie) występuje {GetNumberOfCharactersFromArticleWithSpaces(RecentArticle.Article)} znaków z uwzględnieniem spacji.");
                    Console.WriteLine($"W wybranym artykule (fragmencie) występuje {GetNumberOfCharactersFromArticleOnLyLetters(RecentArticle.Article)} bez znaków spacji.");
                    break;
                case 2:
                    Console.WriteLine($"W wybranym artykule (fragmencie) występuje {GetNumberWordsFromArticle(RecentArticle.Article)} słów.");
                    break;
                case 3:
                    // TODO: zrobic liste kilku/nastu słów
                    // dać możliwośc wyswietlenia topki słow pod względem długości słowa, , zeby pominąć i, lub, że itp.
                    Console.WriteLine($"Top 20# Najczęściej występujące słowo w artykule (fragmencie) to:");
                    int counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article).OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"[{counter++}]#  Słowo:\"{wordWithCounter.Key}\"[{wordWithCounter.Value}]x");  // Print the Repeated word and its count  
                        if(counter > 20) break;
                    }  
            
                    break;
                case 4:
                    if (RecentArticle != null) {
                        await DisplayStatisticMenu();

                    } else {
                        Console.WriteLine("Odwiedź najpierw artykuł.");
                    }
                    break;
                case 5:
                    await DisplayMenu();
                    break;

                case 6:
                    await SearcHEngine();
                    break;
                case 7:
                    Environment.Exit(0);
                    break;
                default:
                    await DisplayStatisticMenu();
                    break;
            }
            restart:
            Console.WriteLine("\t[M] aby wrócić do menu statystyki. ");
            try {
                answer = Console.ReadLine();
                if (answer.ToLower() == "m" || answer == "") await DisplayStatisticMenu();
            } catch (FormatException) {
                goto restart;
            }

        }


            #endregion
       
        #region Wikipedia API ------------------------------------------------------------------------------------------------------------------------------------------------------
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
                newPage.sroffset = (page) * 10;
                search._continue = newPage;
                // Przypisanie zwróconych danych do nowej listy ( dla celów estetycznych, łątwiejszego użycie pożniej w kodzie)
                wynikiWyszukiwania = search.query;
                //Przechwytywanie pustych wyników, i zwrócenie stosownej wiadomości    
                if (wynikiWyszukiwania.search.Count() > 0) {

                    // Wyświetlenie listy tytułów artykułów znalezionych w Wikipedii.
                    int index = 1;
                    foreach (Search item in wynikiWyszukiwania.search) {
                        Console.WriteLine($"[{index++}] {item.title}");
                    }
                    Console.WriteLine($"Strona [{(search._continue.sroffset / 10) + 1} / {((search.query.searchinfo.totalhits / 10) < 1000 ? (search.query.searchinfo.totalhits / 10) : 1000)}]");
                    // Console.WriteLine("Wyszukiwanie zakończone");
                    isSearchingListEmpty = false;

                } else {
                    isSearchingListEmpty = true;
                }
            }
                // Wyświetlenie Pierwszych lini tekstu w artykule
            private async Task GetWikipediaArticleById(int pageId, int length = 5000) {
                // Console.WriteLine("Pobieranie artykułu...");
                // Pobranie odpowiedzi z API Wikipedii jako parametry przekazujemy wczesniej ustalony ID strony 
                //      oraz długość tekstu jaka ma zostać wyświetlona domyślnie 500 znakow.
                string response = await client.GetStringAsync(
                $"https://pl.wikipedia.org/w/api.php?action=query&prop=extracts&exchars={length}&pageids={pageId}&format=json&explaintext=1");
                // Zrzutowanie otrzymanej odpowiedzi na klase Json
                JObject o = JObject.Parse(response);
                // Wyciągnięcie opisu dla danego id strony
                JToken tekstArtykulu = o.SelectToken($"$.query.pages.{pageId}.extract");
                JToken tematArtykulu = o.SelectToken($"$.query.pages.{pageId}.title");
                // Wyświetlenie artykułu.
                Console.WriteLine(tekstArtykulu.ToString());

                // Zapisanie artykulu w pamieci w celow jego szybszej analizy
                SaveArticleForStatisticsPurpose(tematArtykulu.ToString(), tekstArtykulu.ToString());
            }

        private void SaveArticleForStatisticsPurpose(string title, string article) {
            RecentArticle = new RecentReadedArticle(title, article);
        }
        // różne metody pobrania -> podzielone na słowa, na znaki? 
        private RecentReadedArticle GetRecentArticleText() {
            return RecentArticle;
        }

        #endregion

        #region Saving and loaging stuff -------------------------------------------------------------------------------------------------------------------------------------------
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
            Console.WriteLine("Zapisywanie w toku...");
            // Zaimportowanie aktualnie przechowywanej listy z pliku w przypadku gdy ta jest pusta      
            backToStart:
            if (OldHistoryOfSearching.Count == 0) {
                
                try {
                    var jsonFromFile = await File.ReadAllTextAsync(Path.Combine(
                        Environment.CurrentDirectory, "SearchingHistory.txt"));
                    if (jsonFromFile != null)
                        OldHistoryOfSearching = JsonConvert.DeserializeObject<List<SearchingHistory>>(jsonFromFile.ToString());
                } catch (FileNotFoundException) {
                    Console.WriteLine("Uworzenie nowego pustego pliku histori.");
                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(OldHistoryOfSearching);
                    File.WriteAllText(Path.Combine(
                        Environment.CurrentDirectory, "SearchingHistory.txt"),jsonString);
                    // po utworzeniu pliku, wróć się i wykonaj na nim działanie
                   goto backToStart;
                }
            }

            // Sprawdzenie czy historia wyszukiwania zawiera jakiekolwiek obiekty -> nie możeby dodać do listy pustego obiektu
            if (currentHistory.Any()) {
                // Aktualizowanie pliku na koniec działąnia funkcji pooprzez połączenie starej i nowej listy
                //   następnie jej zapisanie do pliku ? nie wiem dlaczego nie moge do nadpisać a sam sie kasuje
                //   więc nieefektywna opcja, pobieranie całości i zapisywanie całości od nowa 
                OldHistoryOfSearching.AddRange(currentHistory);
                Console.WriteLine($"Dodano {currentHistory.Count()} wyswietlone strony.");
                // Po przepisaniu wynikow aktualna liczba wyszukiwan zostaje wyczyszczona
                //   w innym wypadku liczba elementow zapisywanych podczas dzialania aplikacji, by je namnażała
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(OldHistoryOfSearching);
                await File.WriteAllTextAsync(Path.Combine(
                    Environment.CurrentDirectory,"SearchingHistory.txt"),jsonString);
                Console.WriteLine("Zakończono zapisywanie.");
                //czyszczenie podrecznej historii wyszukiwania    
                CurrentHistoryOfSearching = null;
            }else {
                Console.WriteLine("Zakończono - Brak elementow do zapisania.");
            }
        }
        #endregion

        #region Ranking stuff ------------------------------------------------------------------------------------------------------------------------------------------------------
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

        #region Statistics stuff ---------------------------------------------------------------------------------------------------------------------------------------------------

        private static int GetNumberOfCharactersFromArticleWithSpaces(string article) {
            return article.Length;
        }
        private static int GetNumberOfCharactersFromArticleOnLyLetters(string article) {

            return article.Count(c => !Char.IsWhiteSpace(c));
        }
        private static int GetNumberWordsFromArticle(string article) {

            int wordCount = 0, index = 0;

            // skip whitespace until first word
            while (index < article.Length && char.IsWhiteSpace(article[index]))
                index++;

            while (index < article.Length) {
                // check if current char is part of a word
                while (index < article.Length && !char.IsWhiteSpace(article[index]))
                    index++;

                wordCount++;

                // skip whitespace until next word
                while (index < article.Length && char.IsWhiteSpace(article[index]))
                    index++;
            }
            return wordCount;
        }

        private static Dictionary<string,int> GetPopularityOfWordEncounteredInArticle(string article) {
            // krok 1 podzielenie artykulu na osobne słowa
            List<string> artykolListaSlow = new List<string>();
            List<string> artykolListaSlowBezZnakowSpecialnych = new List<string>();
            artykolListaSlow = article.Split(" ").ToList();

            // ktok 2 usunięcie wszystkich przecinkó, kropek, znakow zapytania etc.
            //      dzięki zastosowaniu wyrażenia regularnego dopuszcza tylko litery od a do Z i od 0 do 9
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            foreach (string word in artykolListaSlow) {
                if(rgx.Replace(word, string.Empty) != string.Empty) {
                    artykolListaSlowBezZnakowSpecialnych.Add(rgx.Replace(word.ToLower(), string.Empty));
                }
            }
            // krok 3 pogrupowanie po nazwach i zsumowanie wystąpień
            List<string> sortedList = artykolListaSlowBezZnakowSpecialnych.OrderByDescending(p=>p).ToList();
            Dictionary<string, int> RepeatedWordCount = new Dictionary<string, int>();
            for (int i = 0; i < sortedList.Count; i++) {

                // Check if word already exist in dictionary update the count  
                if (RepeatedWordCount.ContainsKey(sortedList[i])) {
                    int value = RepeatedWordCount[sortedList[i]];
                    RepeatedWordCount[sortedList[i]] = value + 1;
                } else {
                    // if a string is repeated and not added in dictionary , here we are adding   
                    RepeatedWordCount.Add(sortedList[i], 1); 
                }
            }         
            return RepeatedWordCount;
        }
        #endregion
        #region NOTATNIK / TODOs / UWAGI I POMYSŁY ----------------------------------------------------------------------------------------------------------------------------------
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
         * [DONE] TODO: Wyodrebnienie Menu, zeby nie było trzeba przeszukiwać wikipedi zeby sprawdzic ranking/zapisac hisotie itp
         * 
         * 
         * 
         * --------------------------------------------------------------------------------------------------------
         * TODO: 
         * TODO:
         */
        #endregion
    }
}