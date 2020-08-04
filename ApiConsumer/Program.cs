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
        readonly HttpClient client = new HttpClient();
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
                             "\t[3] Pokaz liste najpopularniejszych słow. \n" +
                             "\t[4] Oblicz procentowy udział znaków w artykule. \n" +
                             "\t[5] Wyswietl szczegoly wybranego słowa / znaku \n" +
                             "\t - - - - - - - - - - - - - - - - - - - - - - - - - -\n" +
                             "\t[6] Wróć na stronę główną. \n" +
                             "\t[7] Przejdz do wyszukiwarki. \n"+
                             "\t[8] Zamknij program. \n");
            
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
                    Console.WriteLine($"\tTop 5# Najczęściej występujące słowo w artykule (fragmencie) to:");
                    PrintWordPopularityRanking((int)printingType.liczba_Pozycji,5);
                    back:
                    
                    Console.WriteLine("\tChcesz zmienic parametry wyswietlania ? t/n");
                    answer = Console.ReadLine().ToString().ToLower();
                    if(answer == "t") {
                        backToMenu:
                        Console.WriteLine(  "\t[1] Podaj minimalną długośc wyrazu.\n" +
                                            "\t[2] Podaj maksymalną długośc wyrazu.\n" +
                                            "\t[3] Zmien liczbe wyświetlanych pozycji. (standardowo 5)\n" +
                                            "\t[-] Opcje zmiany sortowania:\n" +
                                            "\t\t[41] Czestotliwośc występowania malejąco,\n" +
                                            "\t\t[42] Częstotliwośc występowania rosnąco,\n" +
                                            "\t\t[43] Sortowanie według wyrazów alfabetycznie rosnąco a-z,\n" +
                                            "\t\t[44] Sortowanie malejąco z-a\n" +
                                            "\t - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"+
                                            "\t[5] Powrót");
                        answer = Console.ReadLine();
                        switch (answer) {
                            case "1":
                                Console.WriteLine("Podaj minimalną długość wyrazu w zestawieniu popularności wystąpień w artykule");
                                answer = Console.ReadLine();
                                PrintWordPopularityRanking((int)printingType.min_Dlugosc, 5, Convert.ToInt32(answer));
                                break;
                            case "2":
                                Console.WriteLine("Podaj maksymalną długość wyrazu w zestawieniu popularności wystąpień w artykule");
                                answer = Console.ReadLine();
                                PrintWordPopularityRanking((int)printingType.max_Dlugosc, 5, Convert.ToInt32(answer));
                                break;

                            case "3":
                                Console.WriteLine("Podaj liczbę wyrazów do wyświetlenia w rankingu: ");
                                answer = Console.ReadLine();
                                PrintWordPopularityRanking((int)printingType.liczba_Pozycji, Convert.ToInt32(answer));
                                break;

                            case "41":  PrintWordPopularityRanking((int)printingType.czestotliwosc_Malejaca); break;

                            case "42":  PrintWordPopularityRanking((int)printingType.czestotliwosc_Rosnaca); break;

                            case "43":  PrintWordPopularityRanking((int)printingType.alfabetycznie_Rosnaco); break;

                            case "44":  PrintWordPopularityRanking((int)printingType.alfabetycznie_Malejaco); break;

                            case "5":   await DisplayStatisticMenu(); break;

                            default:    goto backToMenu;
                        }
                        goto back;
                    }
                    break;
                case 4:
                    PrintWordPopularityRanking((int)printingType.ranking_Liter);

                    break;
                case 5:
                    Console.WriteLine("Podaj słowo (lub jego część), znak, dla któego szukasz informacji. ");
                    answer = Console.ReadLine();
                    PrintWordPopularityRanking((int)printingType.wlasne_Slowo_Lub_Znak, query: answer);
                    break;
                case 6:
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
                Continue newPage = new Continue {
                    sroffset = (page) * 10
                };
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
        private static List<string> CropTextIntoListWithoutSpecialCharacters(string article, int min_dlugosc_wyrazu = 0, int max_dlugosc_wyrazu = 100) {
            // krok 1 podzielenie artykulu na osobne słowa
            List<string> artykolListaSlow;
            List<string> artykolListaSlowBezZnakowSpecialnych = new List<string>();
            artykolListaSlow = article.Split(" ").ToList();

            // krok 2 usunięcie wszystkich przecinkó, kropek, znakow zapytania etc.
            //      dzięki zastosowaniu wyrażenia regularnego dopuszcza tylko litery od a do Z i od 0 do 9
            Regex rgx = new Regex("[^a-zA-Z0-9]");
            foreach (string word in artykolListaSlow) {
                if (rgx.Replace(word, string.Empty) != string.Empty) {
                    string slowo = rgx.Replace(word.ToLower(), string.Empty);
                    if (slowo.Length >= min_dlugosc_wyrazu && slowo.Length <= max_dlugosc_wyrazu) {
                        artykolListaSlowBezZnakowSpecialnych.Add(slowo.Trim());
                    }
                }
            }
            return artykolListaSlow;
        }
        private static Dictionary<string, int> GetPopularityOfWordEncounteredInArticle(string article, int min_dlugosc_wyrazu=0, int max_dlugosc_wyrazu=100) {
            List<string> listaSlow = 
                CropTextIntoListWithoutSpecialCharacters(article, min_dlugosc_wyrazu, max_dlugosc_wyrazu);

            // krok 3 pogrupowanie po nazwach i zsumowanie wystąpień
            Dictionary<string, int> RepeatedWordCount = new Dictionary<string, int>();
            for (int i = 0; i < listaSlow.Count; i++) {

                // Check if word already exist in dictionary update the count  
                if (RepeatedWordCount.ContainsKey(listaSlow[i])) {
                    int value = RepeatedWordCount[listaSlow[i]];
                    RepeatedWordCount[listaSlow[i]] = value + 1;
                } else {
                    // if a string is repeated and not added in dictionary , here we are adding   
                    RepeatedWordCount.Add(listaSlow[i], 1); 
                }
            }         
            return RepeatedWordCount;
        }
        private static Dictionary<char, int> GetPopularityOfCharactersEncounteredInArticle(string article) {
            List<string> listaSlow = CropTextIntoListWithoutSpecialCharacters(article);

            // krok 3 pogrupowanie po nazwach i zsumowanie wystąpień
            Dictionary<char, int> RepeatedCharacterCount = new Dictionary<char, int>();
            for (int i = 0; i < listaSlow.Count; i++) {
                for (int j = 0; j < listaSlow[i].Length; j++) {
                    if (RepeatedCharacterCount.ContainsKey(listaSlow[i][j])) {
                        int value = RepeatedCharacterCount[listaSlow[i][j]];
                        RepeatedCharacterCount[listaSlow[i][j]] = value + 1;
                    } else {
                        // if a string is repeated and not added in dictionary , here we are adding   
                        RepeatedCharacterCount.Add(listaSlow[i][j], 1);
                    }
                }
            }
            return RepeatedCharacterCount;
        }
        private enum printingType
        {
            liczba_Pozycji,
            min_Dlugosc,
            max_Dlugosc,
            czestotliwosc_Malejaca,
            czestotliwosc_Rosnaca,
            alfabetycznie_Malejaco,
            alfabetycznie_Rosnaco,
            ranking_Liter,
            wlasne_Slowo_Lub_Znak
        }
        private static void PrintWordPopularityRanking(int printingType, int liczba_pozycji = 5, int min_dlugosc_wyrazu = 0, int max_dlugosc_wyrazu = 100, string query = null) {
            int counter;
            switch (printingType) {

                #region Wyświetlanie "Liczba_Pozycji" -> przekazanie wartości int odpowiedzialnej za liczbę elementów do wyświetlenia ze słownika popularnych słów
                case 1:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji)
                            break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Min_Dlugosc" -> przekazanie do metody wartości określającej minimalną długość znaku do wyświetlenia w rankingu   
                case 2:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article, min_dlugosc_wyrazu, max_dlugosc_wyrazu)
                                                                            .OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji)
                            break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Max_Dlugosc" -> przekazanie wartości określającej maksymalną długośc słowa do wyświetlenia w rankingu
                case 3:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article, min_dlugosc_wyrazu, max_dlugosc_wyrazu)
                                                                            .OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji)
                            break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Czestotliwosc_Malejaca" -> Wyswietlenie standardowych 5 elementow posortowanych malejąco (od najpopularniejszego)
                case 4:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Czestotliwosc_Rosnaca" -> Wyswietlenie standardowych 5 elementow posortowanych rosnąco (od najmniej popularnych)

                case 5:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderBy(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Alfabetycznie_Malejaco" -> Wyświetlenie standardowych 5 elementów posortowanych alfabetycznie malejąco (od Z-A)
                case 6:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article, liczba_pozycji)
                                                                            .OrderBy(p => p.Key)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Alfabetycznie_Rosnaco" -> Wyświetlenie standardowych 5 elementów posortowanych alfabetycznie rosnąco (od A-Z)
                case 7:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderByDescending(p => p.Key)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) break;
                    }
                    break;
                #endregion

                #region Wyświetlanie "Ranking_Liter" -> Wyswietlenie liczby wystąpień liter alfabetu bez uwzględnienia znaków specialnych, oraz wyswietlenie ich procentowej zawartosci w tekscie

                case 8:
                    counter = 1;
                    int charactersInArticle = GetNumberOfCharactersFromArticleOnLyLetters(RecentArticle.Article);
                    foreach (KeyValuePair<char, int> characterCounter in GetPopularityOfCharactersEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderByDescending(p => p.Value)) {
                        double udzialProcentowy = (characterCounter.Value * 100.0) / charactersInArticle;
                        Console.WriteLine($"\t[{counter++,2}]# \"{characterCounter.Key,2} \"  [{characterCounter.Value,2}x] [{Math.Round(udzialProcentowy, 2),4}%]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) { break; }
                    }
                    Console.WriteLine("Chcesz wyświetlić wszystkie wyniki? t/n");
                    string answer = Console.ReadLine().ToLower();
                    counter = 1;
                    if (answer == "t") {
                        foreach (KeyValuePair<char, int> characterCounter in GetPopularityOfCharactersEncounteredInArticle(RecentArticle.Article)
                                                        .OrderByDescending(p => p.Value)) {
                            float udzialProcentowy = (characterCounter.Value * 100.0f) / charactersInArticle;
                            if (counter < 2) {
                                counter++;
                                Console.Write($"| [\"{characterCounter.Key,2} \"] => [{characterCounter.Value,2}x({Math.Round(udzialProcentowy, 2),4}%)] | ");
                            } else {
                                counter = 1;
                                Console.Write($"[\"{characterCounter.Key,2} \"] => [{characterCounter.Value,2}x({Math.Round(udzialProcentowy, 2),4}%)] |\n");
                            }
                        }
                    }
                    break;
                #endregion

                #region Wyświetlanie "wlasne_Slowo_Lub_Znak" -> przekazanie wprowadzonej wartości string (mogącą być słowem, częścią słowa, lub znakiem) następnie wyświetlenie wszystkich pasujących wystąpień / dla znaku jest to tylko 1 element
                case 9:
                    // krok 1 sprawdzic czy uzytkownik wpisal slowo czy znak
                    foreach (KeyValuePair<string, int> specifiedWord in GetPopularityByPassingWordOrCharacter(article: RecentArticle.Article, word: query)
                                                                            .OrderByDescending(p => p.Key)) {
                        Console.WriteLine($"\t[\"{specifiedWord.Key,10} \"] [{specifiedWord.Value,2} x]");  // Print the Repeated word and its count  
                    }
                    break;
                #endregion

                #region Wyświetlanie "" -> standardowe wyswietlenie 5 najpopularniejszych słow z artkulu bez okreslonego sortowania ani ograniczen
                default:
                    counter = 1;
                    foreach (KeyValuePair<string, int> wordWithCounter in GetPopularityOfWordEncounteredInArticle(RecentArticle.Article)
                                                                            .OrderByDescending(p => p.Value)) {
                        Console.WriteLine($"\t[{counter++,2}]# \"{wordWithCounter.Key,10} \"  [{wordWithCounter.Value,2}x]");  // Print the Repeated word and its count  
                        if (counter > liczba_pozycji) break;
                    }
                    break;
                    #endregion

            }
        }
        private static Dictionary<string, int> GetPopularityByPassingWordOrCharacter(string word, string article) {
            if (word.Length > 1) {
                Dictionary<string, int> listOfWords = GetPopularityOfWordEncounteredInArticle(article);
                Dictionary<string, int> pasujaceSlowaZArtykulu = new Dictionary<string, int>();
                foreach (var slowo in listOfWords) {
                    if (slowo.Key.ToLower().Contains(word.ToLower())) {
                        pasujaceSlowaZArtykulu.Add(slowo.Key,slowo.Value);
                    }
                }
                return pasujaceSlowaZArtykulu;
            } else {
                string character = word.ElementAt(0).ToString();
                var listOfChars = GetPopularityOfCharactersEncounteredInArticle(article);
                Dictionary<string, int> znakZWystapieniemWTekscie = new Dictionary<string, int>();
                int sumaWystapienznaku = listOfChars.Where(p => p.Key.ToString() == character).FirstOrDefault().Value;
                znakZWystapieniemWTekscie.Add(character, sumaWystapienznaku);
                return znakZWystapieniemWTekscie;

            }
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