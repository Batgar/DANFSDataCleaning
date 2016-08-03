using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using edu.stanford.nlp.ie.crf;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using edu.stanford.nlp.sequences;
using edu.stanford.nlp.ling;
using edu.stanford.nlp.process;
using System.Xml.XPath;

namespace DANFS.PreProcessor
{
    class PreprocessorMain
    {
        CRFClassifier classifier;

        string ROOT_PATH = @"C:\Users\Dan Edgar\Documents";

       
        public void DoFindShipAssociations()
        {
            //Check to see if the element is an "i" element, and doesn't start with the current ship name.
            //If it is, then it could be a link to a ship, we GUID it up, and then try to find a matching ship from the
            //master ship dictionary.
        }

        private SQLiteConnection CreateNewDateDatabase()
        {
            var dateDatabasePath = System.IO.Path.Combine(ROOT_PATH, @"shipdates.sqlite");

            File.Delete(dateDatabasePath);

            SQLiteConnection.CreateFile(dateDatabasePath);

            var dateConnection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dateDatabasePath));
            dateConnection.Open();

            string createTableSql = "create table shipdate (id text, date_guid text, year text, month text, day text, preview text, url text, title text, subtitle text)";

            SQLiteCommand createTableCommand = new SQLiteCommand(createTableSql, dateConnection);
            createTableCommand.ExecuteNonQuery();

            return dateConnection;
        }

        private SQLiteConnection CreateNewAugmentedDatabase()
        {
            var pathToAugmentedDANFSDatabase = System.IO.Path.Combine(ROOT_PATH, "danfs-augmented.sqlite3");
            File.Delete(pathToAugmentedDANFSDatabase);

            SQLiteConnection.CreateFile(pathToAugmentedDANFSDatabase);

            var augmentedConnection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", pathToAugmentedDANFSDatabase));
            augmentedConnection.Open();

            string createTableSql = "create table danfs_ships (id text, url text, title text, subtitle text, history text)";

            SQLiteCommand createTableCommand = new SQLiteCommand(createTableSql, augmentedConnection);
            createTableCommand.ExecuteNonQuery();

            return augmentedConnection;
        }

        private SQLiteConnection OpenExistingAugmentedDatabase()
        {
            var pathToAugmentedDANFSDatabase = System.IO.Path.Combine(ROOT_PATH, "danfs-augmented.sqlite3");
           
            var augmentedConnection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", pathToAugmentedDANFSDatabase));
            augmentedConnection.Open();

            return augmentedConnection;
        }

        public void PostProcessDates(SQLiteDataReader reader, XDocument doc, SQLiteConnection dateConnection)
        {

            //Log the fileName as the ID of the ship.
            //Search through the XML looking for
            // - <date year="1795" month="August" day="15">15 August 1795</date>
            // Add a UDID per date.
            // Log the GUID, ID of ship, year, month, day, into SQLite.
            // Then we should be able to query the SQLite DB by the above, find the relevant date in the XML, and print out the paragraph from the doc as needed.
            //var doc = XDocument.Load(fileName);

            using (var transaction = dateConnection.BeginTransaction())
            {

                foreach (var date in doc.Descendants("date"))
                {
                    var dateGuid = Guid.NewGuid();
                    date.Add(new XAttribute("date_guid", dateGuid.ToString()));

                    using (SQLiteCommand insertCommand = new SQLiteCommand("insert into shipdate (id, date_guid, year, month, day, preview, url, title, subtitle) values (@id, @date_guid, @year, @month, @day, @preview, @url , @title, @subtitle)", dateConnection))
                    {

                        insertCommand.Parameters.Add(new SQLiteParameter("@id", reader["id"]));
                        insertCommand.Parameters.Add(new SQLiteParameter("@date_guid", dateGuid.ToString()));

                        var year = date.Attribute("year") != null ? date.Attribute("year").Value : string.Empty;
                        insertCommand.Parameters.Add(new SQLiteParameter("@year", year));

                        var month = date.Attribute("month") != null ? date.Attribute("month").Value : string.Empty;
                        insertCommand.Parameters.Add(new SQLiteParameter("@month", month));

                        var day = date.Attribute("day") != null ? date.Attribute("day").Value : string.Empty;
                        insertCommand.Parameters.Add(new SQLiteParameter("@day", day));

                        var preview = GeneratePreview(date);
                        insertCommand.Parameters.Add(new SQLiteParameter("@preview", preview));

                        insertCommand.Parameters.Add(new SQLiteParameter("@url", reader["url"]));
                        insertCommand.Parameters.Add(new SQLiteParameter("@title", reader["title"]));
                        insertCommand.Parameters.Add(new SQLiteParameter("@subtitle", reader["subtitle"] != null ? reader["subtitle"] : string.Empty));

                        insertCommand.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        private void AddToAugmented(SQLiteDataReader reader, XDocument augmentedDoc, SQLiteConnection augmentedConnection)
        {
            //Also lay down the changed XML into a new danfs-augmented.sqlite3 database.
            using (SQLiteCommand insertCommand = new SQLiteCommand("insert into danfs_ships (id, url, title, subtitle, history) values (@id, @url, @title, @subtitle, @history)", augmentedConnection))
            {

                insertCommand.Parameters.Add(new SQLiteParameter("@id", reader["id"]));
                insertCommand.Parameters.Add(new SQLiteParameter("@url", reader["url"]));
                insertCommand.Parameters.Add(new SQLiteParameter("@title", reader["title"]));
                insertCommand.Parameters.Add(new SQLiteParameter("@subtitle", reader["subtitle"] != null ? reader["subtitle"] : string.Empty));
                insertCommand.Parameters.Add(new SQLiteParameter("@history", augmentedDoc.ToString()));

                insertCommand.ExecuteNonQuery();
            }
        }

        private string GeneratePreview(XElement element)
        {
            var previousTextNode = element.PreviousNode as XText;

            var nextTextNode = element.NextNode as XText;

            var prevText = previousTextNode != null ? previousTextNode.Value : string.Empty;
            var nextText = nextTextNode != null ? nextTextNode.Value : string.Empty;

            return $"{prevText}{element.Value}{nextText}";
        }

        public async void Process()
        {
            
            System.Diagnostics.Trace.Listeners.Add(new PreProcessTracing(Path.Combine(ROOT_PATH, "DANFSPreProcessLog.txt")));

            //DoNamedEntityRecognition(false);
            //return;

            //MakeLocationDictionary();
            //return;

            //await GeocodeUniqueLocations();
            //return;

            //TryGetRanksOfPeople();
            //return;


            var pathToMainDANFSDatabase = System.IO.Path.Combine(ROOT_PATH, "danfs.sqlite3");
            var connection = new SQLiteConnection(string.Format("Data Source={0};Version=3", pathToMainDANFSDatabase));
            connection.Open();
            var command = new SQLiteCommand("select * from danfs_ships", connection);
            var manifestReader = command.ExecuteReader();
            CreateManifest(manifestReader);

            int totalShipCount = 0;
            int missingShipRegistries = 0;
            totalDates = 0;

            int shipHistoryDividerCount = 0;

          


           

            using (var dateConnection = CreateNewDateDatabase())
            //using (var augmentedConnection = CreateNewAugmentedDatabase())
            {
                var reader = command.ExecuteReader();

                long totalMilliseconds = 0;

                //Generates all files and inserts dates.
                while (reader.Read())
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    //try
                    {
                        var doc = XDocument.Parse("<root>" + (string)reader["history"] + "</root>");


                        var processedValue = doc.Root.Attribute("date");

                        if (processedValue != null && processedValue.Value == "true")
                        {
                            continue;
                        }

                       

                        XElement rootElement = new XElement("root");

                        rootElement.Add(new XAttribute("source_title", reader["title"]));
                        rootElement.Add(new XAttribute("source_subtitle", reader["subtitle"] == null ? string.Empty : reader["subtitle"]));
                        rootElement.Add(new XAttribute("source_url", reader["url"]));
                        rootElement.Add(new XAttribute("source_id", reader["id"]));

                        lastYear = string.Empty;

                        shipRegistryElement = null;

                        noYearDateElements.Clear();

                        var context = new ProcessElementContext();
                        context.Title = reader["title"] as String;

                        ProcessElement(reader["id"] as String, doc.Root, rootElement, context);

                        if (!context.FirstAlternateShipHistoryDiscovered)
                        {
                            System.Diagnostics.Trace.WriteLine($"ALTERNATE SHIP HISTORY DIVIDER NOT FOUND FOR {reader["id"]}");
                        }
                        else
                        {
                            shipHistoryDividerCount++;
                        }

                        var augmentedDoc = new XDocument();
                        augmentedDoc.Add(rootElement);

                        if (shipRegistryElement == null)
                        {
                            System.Diagnostics.Trace.WriteLine($"No ship registry for {reader["id"]}", "INFO");
                            missingShipRegistries++;
                        }

                        //Throw in our flag so we can key off later if we decide to do processing differently.


                        PostProcessDates(reader, augmentedDoc, dateConnection);

                        rootElement.Add(new XAttribute("date", "true"));

                        //AddToAugmented(reader, augmentedDoc, augmentedConnection);

                        var shipFileName = System.IO.Path.Combine(ROOT_PATH, $@"Ships\{reader["id"]}.xml");
                        augmentedDoc.Save(shipFileName);

                        totalShipCount++;

                    }
                    stopwatch.Stop();

                    totalMilliseconds += stopwatch.ElapsedMilliseconds;

                    System.Diagnostics.Trace.WriteLine($"Current Estimated Initial Processing Time: {totalMilliseconds / totalShipCount * 11000 / 1000 / 3600} hours");

                    /*catch (Exception e)
                    {
                        System.Diagnostics.Trace.WriteLine($"Error while processing ship {reader["id"]} - {e.Message}", "ERROR");
                    }*/
                }
            }

            DoNamedEntityRecognition(true);   

            System.Diagnostics.Trace.WriteLine($"Total Ships: {totalShipCount} -- Missing Registries: {missingShipRegistries} - Total Dates Logged: {totalDates} - Total ship history dividers found: {shipHistoryDividerCount}", "INFO");

        }

        private void DoNamedEntityRecognition(bool startNew)
        {

            // Path to the folder with classifiers models
            var jarRoot = @"C:\Users\Dan Edgar\Downloads\stanford-ner-2015-12-09\stanford-ner-2015-12-09";
            var classifiersDirecrory = System.IO.Path.Combine(jarRoot, @"classifiers");

            
            classifier = CRFClassifier.getClassifierNoExceptions(
                Path.Combine(classifiersDirecrory,
                    @"english.all.3class.distsim.crf.ser.gz" // Loading 3 class classifier model
            ));

            var pathToMainDANFSDatabase = System.IO.Path.Combine(ROOT_PATH, "danfs.sqlite3");
            var connection = new SQLiteConnection(string.Format("Data Source={0};Version=3", pathToMainDANFSDatabase));
            connection.Open();
            var command = new SQLiteCommand("select * from danfs_ships", connection);
            var reader = command.ExecuteReader();

            SQLiteConnection augmentedConnection = null;

            if (startNew)
            {
                augmentedConnection = CreateNewAugmentedDatabase();
            }
            else
            {
                augmentedConnection = OpenExistingAugmentedDatabase();
            }

            using (augmentedConnection)
            {
                long totalMilliseconds = 0;
                int totalShipCount = 0;

                while (reader.Read())
                {
                    var currentShipXMLFilePath = System.IO.Path.Combine(ROOT_PATH, $@"Ships\{reader["id"] as string}.xml");

                    var doc = XDocument.Load(currentShipXMLFilePath);

                    var processedValue = doc.Root.Attribute("personLocationOrganization");


                    if (processedValue != null && processedValue.Value == "true")
                    {
                        System.Diagnostics.Trace.WriteLine($"Skipping NER - {reader["id"]}");
                        continue;
                    }

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                   

                   

                    XElement rootElement = new XElement("root");

                    //Copy all existing root attributes to the new doc.
                    foreach (var attribute in doc.Root.Attributes())
                    {
                        rootElement.Add(attribute);
                    }

                   

                    ProcessPersonLocationOrganization(reader["id"] as string, doc.Root, rootElement);

                    //Throw in our flag so we don't double process!
                    rootElement.Add(new XAttribute("personLocationOrganization", "true"));

                    var augmentedDoc = new XDocument();
                    augmentedDoc.Add(rootElement);

                    var currentShipXMLFilePathNER = System.IO.Path.Combine(ROOT_PATH, $@"ShipsNER\{reader["id"] as string}.xml");
                    augmentedDoc.Save(currentShipXMLFilePathNER);

                    AddToAugmented(reader, augmentedDoc, augmentedConnection);

                    stopwatch.Stop();

                    totalMilliseconds += stopwatch.ElapsedMilliseconds;
                    totalShipCount++;

                    System.Diagnostics.Trace.WriteLine($"Current Estimated Classification Processing Time: {totalMilliseconds / totalShipCount * 11000 / 1000 / 3600} hours");
                }
            }

            /*var fileCount = fileNames.Length;
            {
                bool keepGoing = true;
                int chunkSize = 500;

                var fileChunk = fileNames.Take(chunkSize);

                List<Task> allRunningTasks = new List<Task>();

                allRunningTasks.AddRange(GetNextTaskBank(fileChunk, out keepGoing, chunkSize));

                int processedCount = allRunningTasks.Count;

                Task[] taskPool = allRunningTasks.ToArray();

                do
                {
                    var completedTaskIndex = Task.WaitAny(taskPool);
                    processedCount += 1;
                    if (processedCount >= fileCount)
                    {
                        break;
                    }

                    var nextTaskBank = GetNextTaskBank(fileNames.Skip(processedCount).Take(1), out keepGoing, 1);
                    if (keepGoing)
                    {
                        taskPool[completedTaskIndex] = nextTaskBank[0];
                    }

                } while (keepGoing);

            }*/
        }

        private void CreateManifest(SQLiteDataReader manifestReader)
        {
            //Generates a JSON manifest
            List<ShipManifestEntry> shipManifestEntries = new List<ShipManifestEntry>();
            while (manifestReader.Read())
            {
                shipManifestEntries.Add(new ShipManifestEntry()
                {
                    ID = manifestReader["id"] as string,
                    Title = manifestReader["title"] as string,
                    URL = manifestReader["url"] as string,
                    Subtitle = manifestReader["subtitle"] == null ? string.Empty : manifestReader["subtitle"] as string
                });
            }
            //Serialize all to a JSON file.

            string manifestJSONPath = System.IO.Path.Combine(ROOT_PATH, @"Ships\manifest.json");

            if (File.Exists(manifestJSONPath))
            {
                File.Delete(manifestJSONPath);
            }

            System.IO.File.WriteAllText(manifestJSONPath, JsonConvert.SerializeObject(shipManifestEntries));

            manifestReader.Close();
        }

        Task[] GetNextTaskBank(IEnumerable<string> files, out bool keepGoing, int chunkSize)
        {
            var tasks = new List<Task>();

            keepGoing = true;
            
            foreach (var file in files)
            {
                tasks.Add(Task.Run(new Action(() =>
                {
                    var doc = XDocument.Load(file);

                    XElement rootElement = new XElement("root");

                    var processedValue = doc.Root.Attribute("personLocationOrganization");

                    //Copy all existing root attributes to the new doc.
                    foreach (var attribute in doc.Root.Attributes())
                    {
                        rootElement.Add(attribute);
                    }

                    if (processedValue != null && processedValue.Value == "true")
                    {
                        return;
                    }

                    ProcessPersonLocationOrganization(Path.GetFileNameWithoutExtension(file), doc.Root, rootElement);

                    //Throw in our flag so we don't double process!
                    rootElement.Add(new XAttribute("personLocationOrganization", "true"));



                    var augmentedDoc = new XDocument();
                    augmentedDoc.Add(rootElement);
                    augmentedDoc.Save(System.IO.Path.Combine(ROOT_PATH, $@"Ships\{Path.GetFileNameWithoutExtension(file)}.xml"));
                })));
            }                

            keepGoing = tasks.Count() == chunkSize;

            return tasks.ToArray();
        }

        string[] possibleRanks = new string[] {
                "Miss",
                "Mrs.",
                "Lt.",
                "Ens.",
                "Assistant Surgeon",
                "Surgeon",
                "Rear Admiral",
                "Lt. Comdr.",
                "Lt. (j.g.)",
                "Master",
                "Comdr.",
                "Flag Officer",
                "General",
                "President",
                "Capt.",
                "Vice Admiral",
                "Commodore",
                "Secretary",
                "Lt. (jg.)",
                "Lt. (jg-)",
                "Rear Adm.",
                "Adm.",
                "Governor",
                "Vice Adm.",
                "Sergeant",
                "Electrician’s Mate 1st Class",
                "Technician Fireman Apprentice",
                "Prince",
                "Brig. Gen.",
                "Lt. Gen.",
                "Maj. Gen.",
                "Major General",
                "Electrician Fireman",
                "Fireman",
                "Mate 3rd Class",
                "Secretary of Defense",
                "Secretary of",
                "Chief Electronics Technician",
                "Warfare Systems Operator",
                "Gen.",
                "Seaman",
                "Tactical Coordinator Lt.",
                "Dr.",
                "Chief",
                "Mate",
                "Technician 3rd Class",
                "Disposal Senior Chief",
                "Acting Ensign",
                "Acting Master",
                "Mr.",
                "Lt. Col.",
                "Secretary of the Treasury",
                "Radioman 1st Class",
                "General",
                "Queen",
                "Acting Volunteer Lt.",
                "Fleet Admiral",
                "Brigadier General",
                "Sir",
                "Assistant Surgeon",
                "Acting",
                "Private, First Class",
                "Quartermaster 1st Class",
                "Mate 2d Class",
                "Metalsmith 2d Class",
                "Seaman 2nd class",
                "Seaman 1st Class",
                "Acting Vol. Lt",
                "Vice Admiral",
                "Admiral",
                "Representative",
                "Senator",
                "Sen.",
                "Officer 2d Class",
                "Motor Machinist's Mate",
                "Chief Commissioner",
                "Commissioner",
                "Acting Assistant Paymaster",
                "Paymaster",
                "Honorable",
                "Maj.",
                "Major",
                "Quartermaster",
                "Admiral Sir",
                "Platoon Sergeant",
                "Sgt.",
                "Acting Master",
                "Master",
                "Leading Operator Mechanic",
                "Major General Commandant",
                "Commandant",
                "Naval Constructor",
                "Chief Torpedoman",
                "Mate 2d Class",
                "Second Master",
                "Master",
                "First Master",
                "Governor",
                "Captain",
                "Commander",
                "Lieutenant",
                "Lieutenant Commander",
                "Seaman 1st Class",
                "Chief Gunner",
                "Sailing Master",
                "Col.",
                "Lord",
                "Naval Constructor",
                "ex-President",
                "Ambassador",
                "Assistant Engineer",
                "Master Commandant",
                "King",
                "Empress",
                "Chief Boatswain's Mate",
                "Congressman",
                "Chief Engineer",
                "Chief",
                "Lieutenant Colonel",
                "1st Lt.",
                "Radioman 3d Class",
                "Acting Volunteer Lieutenant",
                "Mate",
                "Navigator",
                "Counsellor",
                "Princess",
                "Crown Princess",
                "Chief Boatswain",
                "Boatswain",
                "Chief Boatswain’s Mate",
                "Lieutenant General",
                "Lt. Gen.",
                "Lady",
        };

        string[] misparsedPersonPrefixShouldBeLocation = new string[] {
            "NS",
            "Holy",
        "Point",
        "Ste.",
        "Battery",
        "Fort",
        "Port",
        "Capes",
        "Cape",
        };

        string[] ambiguousLocationPerson = new string[]
        {
            "St.",
            "Saint",
        };

        string[] misparsedPerson = new string[]
        {
            "Big",
            "Typhoons",
            "Typhoon",
            "Hurricane",
            "Hurricanes",
        };

        //Cataloging alternate LOCATION types that we don't want to map, but we don't want to lose either.
        Dictionary<string, string> locationMarkers = new Dictionary<string, string>()
                    {
                        { "Atlantic" , "Ocean" },
                        { "Pacific", "Ocean" },
                        { "Indian", "Ocean" },
                        { "North America", "Continent" },
                        { "South America", "Continent" },
                        { "Africa", "Continent" },
                        { "Europe", "Continent" },
                        { "Asia", "Continent" },
                        { "Antarctica", "Continent" },
                        { "Australia", "Continent" },
                        { "Arctic", "Region" },
                        { "United States", "Country" },
                        { "California", "Region" },
                    };

        string[] invalidLocations = new string[] { "United States Navy", "U.S.S" };


        private void TryGetRanksOfPeople()
        {
            List<string> possibleRanks = new List<string>();

            //Now we will produce a location dictionary from all locations, and dump the JSON.
            foreach (var file in Directory.GetFiles(System.IO.Path.Combine(ROOT_PATH, "Ships"), "*.xml"))
            {
                var doc = XDocument.Load(file);

                foreach (var personElement in doc.Root.Descendants("PERSON"))
                {

                    //TODO: Only do this for now to get the master location list, skip anything in an italics node due to ship name formatting (i.e. New Jersey - Battleship)
                    var textNodeBeforePerson = personElement.NodesBeforeSelf().LastOrDefault();

                    if (textNodeBeforePerson != null && textNodeBeforePerson is XText)
                    {
                        var textNode = textNodeBeforePerson as XText;
                        var textNodeValue = textNode.Value.Trim();

                        //Get the last 3 words in the text, and write it out.
                        var allWordsBySpace = textNodeValue.Split(' ');

                        var lastThreeWords = allWordsBySpace.Where((o, i) => i > allWordsBySpace.Length - 4).ToArray();

                        possibleRanks.Add(string.Join(" ", lastThreeWords));

                    }
                }
            }

            File.WriteAllLines(System.IO.Path.Combine(ROOT_PATH, @"ShipsStats\AllPossibleRanks.txt"), possibleRanks.ToArray());
        }

        private SQLiteConnection CreateNewLocationDatabase()
        {
            var locationDatabasePath = System.IO.Path.Combine(ROOT_PATH, @"shiplocations.sqlite");

            File.Delete(locationDatabasePath);

            SQLiteConnection.CreateFile(locationDatabasePath);

            var locationConnection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", locationDatabasePath));
            locationConnection.Open();

            string createTableSql = "create table locationJSON (name text, geocodeJSON text)";

            SQLiteCommand createTableCommand = new SQLiteCommand(createTableSql, locationConnection);
            createTableCommand.ExecuteNonQuery();

            string createShipLocationDateTable = "create table shipLocationDate (shipID text, locationname text, startdate text, enddate text, locationguid text)";
            SQLiteCommand createTableCommand2 = new SQLiteCommand(createShipLocationDateTable, locationConnection);
            createTableCommand2.ExecuteNonQuery();

            return locationConnection;
        }

        SQLiteConnection OpenExistingLocationDatabase(bool recreateShipLocationDateTable)
        {
            var locationDatabasePath = System.IO.Path.Combine(ROOT_PATH, @"shiplocations.sqlite");

            var locationConnection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", locationDatabasePath));
            locationConnection.Open();

            if (recreateShipLocationDateTable)
            {
                try
                {
                    string dropShipLocationDateTable = "drop table shipLocationDate";
                    SQLiteCommand dropShipLocationDateTableCommand = new SQLiteCommand(dropShipLocationDateTable, locationConnection);
                    dropShipLocationDateTableCommand.ExecuteNonQuery();
                }
                catch
                {

                }

                string createShipLocationDateTable = "create table shipLocationDate (shipID text, locationname text, startdate text, enddate text, locationguid text)";
                SQLiteCommand createTableCommand2 = new SQLiteCommand(createShipLocationDateTable, locationConnection);
                createTableCommand2.ExecuteNonQuery();
            }
            return locationConnection;
        }

        XElement FindElementBefore(XElement elementToStartAt, string elementName)
        {
            //First check siblings.
            var closestPreviousElement = elementToStartAt.ElementsBeforeSelf(elementName)?.LastOrDefault();
            if (closestPreviousElement != null)
            {
                return closestPreviousElement;
            }
            else
            {
                //Check the parents siblings in reverse order.
                foreach (var parentSibling in elementToStartAt.Parent.ElementsBeforeSelf().Reverse())
                {
                    var closestElement = parentSibling.Elements(elementName)?.LastOrDefault();
                    if (closestElement != null)
                    {
                        //System.Diagnostics.Trace.WriteLine($"Found {elementName} element before in a previous paragraph");
                        return closestElement;
                    }
                }
            }
            return null;            
        }

        XElement FindElementAfter(XElement elementToStartAt, string elementName)
        {
            //First check siblings.
            var closestNextElement = elementToStartAt.ElementsAfterSelf(elementName)?.FirstOrDefault();
            if (closestNextElement != null)
            {
                return closestNextElement;
            }
            else
            {
                //Check the parents siblings in reverse order.
                foreach (var parentSibling in elementToStartAt.Parent.ElementsAfterSelf())
                {
                    var closestElement = parentSibling.Elements(elementName)?.FirstOrDefault();
                    if (closestElement != null)
                    {
                        
                        return closestElement;
                    }
                }
            }
            return null;
        }

        DateTime GetSeasonalDateTimeFromElement(XElement seasonalDateElement, string shipID)
        {
            var season = seasonalDateElement.Attribute("season").Value.ToLowerInvariant();
            var year = seasonalDateElement.Attribute("year")?.Value;
            if (string.IsNullOrEmpty(year))
            {
                System.Diagnostics.Trace.WriteLine($"INVALID SEASON YEAR: {season} {seasonalDateElement} {shipID}");
                return DateTime.MinValue;
            }

            switch (season)
            {
                case "fall":
                    return DateTime.Parse($"December 20, {year}");
                case "winter":
                    return DateTime.Parse($"March 19, {year}");
                case "spring":
                    return DateTime.Parse($"June 21, {year}");
                case "summer":
                    return DateTime.Parse($"September 21, {year}");
                default:
                    System.Diagnostics.Trace.WriteLine($"INVALID SEASON: {season} {seasonalDateElement} {shipID}");
                    return DateTime.MinValue;
            }
        }
        
        DateTime GetDateTimeFromElement(XElement closestPreviousDateElement, string shipID, bool isNextDate)
        {

            if (closestPreviousDateElement.Attribute("season") != null)
            {
                return GetSeasonalDateTimeFromElement(closestPreviousDateElement, shipID);
            }

            //We have a location, a ship ID, and possible associated dates.
            //Log the location Guid, doc id, location name, before date, end date, 
            var prevYear = closestPreviousDateElement.Attribute("year");
            var prevMonth = closestPreviousDateElement.Attribute("month");
            var prevDay = closestPreviousDateElement.Attribute("day");

            if (prevMonth != null && prevMonth.Value == "November" &&
               prevDay != null && Convert.ToInt32(prevDay.Value) > 30)
            {
                prevDay = new XAttribute("day", "30");
            }

            if (prevMonth != null && prevMonth.Value == "February" &&
              prevDay != null && Convert.ToInt32(prevDay.Value) > 28)
            {
                prevDay = new XAttribute("day", "28");
            }

            if (prevMonth != null && prevMonth.Value == "June" &&
              prevDay != null && Convert.ToInt32(prevDay.Value) > 30)
            {
                prevDay = new XAttribute("day", "30");
            }


            if (prevMonth != null && prevMonth.Value == "April" &&
              prevDay != null && Convert.ToInt32(prevDay.Value) > 30)
            {
                prevDay = new XAttribute("day", "30");
            }

            if (prevMonth != null && prevMonth.Value == "September" &&
            prevDay != null && Convert.ToInt32(prevDay.Value) > 30)
            {
                prevDay = new XAttribute("day", "30");
            }

            if (prevMonth != null && prevMonth.Value == "May" &&
          prevDay != null && Convert.ToInt32(prevDay.Value) > 30)
            {
                prevDay = new XAttribute("day", "30");
            }

            if (prevMonth != null && prevMonth.Value == "August" &&
          prevDay != null && Convert.ToInt32(prevDay.Value) > 31)
            {
                prevDay = new XAttribute("day", "31");
            }

            if (prevDay != null && prevDay.Value == "0")
            {
                prevDay = new XAttribute("day", 1);
            }

            try
            {
                DateTime beginDate = DateTime.MinValue;

                if (prevYear != null && prevMonth != null && prevDay != null)
                {
                    beginDate = DateTime.Parse($"{prevMonth.Value} {prevDay.Value.Trim(new char[] { '_', '*' })},{prevYear.Value}");
                }
                else if (prevYear != null && prevMonth != null)
                {
                    beginDate = DateTime.Parse($"{prevMonth.Value} 1, {prevYear.Value}");
                }
                else if (prevYear != null)
                {
                    if (isNextDate)
                    {
                        beginDate = DateTime.Parse($"December 31, {prevYear.Value}");
                    }
                    else
                    {
                        beginDate = DateTime.Parse($"January 1, {prevYear.Value}");
                    }
               }

                return beginDate;
            }
            catch (Exception e)
            {
                //Rogue parsing situation! Log it for future fixes!
                System.Diagnostics.Trace.WriteLine($"Invalid date: {closestPreviousDateElement} {shipID} {e.Message}");
            }

            return DateTime.MinValue;
        }
        

        private async void MakeLocationDictionary()
        {

            using (var locationConnection = OpenExistingLocationDatabase(true))
            {

                List<string> uniqueLocations = new List<string>();

                int shipCount = 0;

                //Now we will produce a location dictionary from all locations, and dump the JSON.
                foreach (var file in Directory.GetFiles(System.IO.Path.Combine(ROOT_PATH, @"ShipsNER"), "*.xml"))
                {
                    var doc = XDocument.Load(file);

                    //TODO: Only do this for now to get the master location list, skip anything in an italics node due to ship name formatting (i.e. New Jersey - Battleship)
                    var locations = doc.Root.Descendants("LOCATION").Where(e => e.Parent.Name != "i" && !uniqueLocations.Contains(e.Value)).Select(e => e.Value).Distinct();

                    uniqueLocations.AddRange(locations);


                    var shipID = Path.GetFileNameWithoutExtension(file);

                    using (var transaction = locationConnection.BeginTransaction())
                    {

                        foreach (var location in doc.Root.Descendants("LOCATION"))
                        {
                            var closestPreviousDateElement = FindElementBefore(location, "date"); // location.ElementsBeforeSelf("date")?.LastOrDefault();
                            var closestNextDateElement = FindElementAfter(location, "date"); //location.ElementsAfterSelf("date")?.FirstOrDefault();

                            if (closestNextDateElement == null && 
                                closestPreviousDateElement == null)
                            {
                                System.Diagnostics.Trace.WriteLine($"No next date and prev date for location {shipID} - {location.Value} - {location.Attribute("location_guid").Value}");
                            }
                            else if (closestNextDateElement == null)
                            {
                                System.Diagnostics.Trace.WriteLine($"No next date location {shipID} - {location.Value} - {location.Attribute("location_guid").Value}");
                            }
                            else if (closestPreviousDateElement == null)
                            {
                                System.Diagnostics.Trace.WriteLine($"No prev date location {shipID} - {location.Value} - {location.Attribute("location_guid").Value}");
                            }


                            DateTime beginDate = DateTime.MinValue;
                            DateTime endDate = DateTime.MinValue;

                            if (closestPreviousDateElement != null)
                            {
                                beginDate = GetDateTimeFromElement(closestPreviousDateElement, shipID, false);
                            }

                            if (closestNextDateElement != null)
                            {
                                endDate = GetDateTimeFromElement(closestNextDateElement, shipID, true);                                
                            }

                            if (beginDate != DateTime.MinValue && endDate != DateTime.MinValue)
                            {
                                //System.Diagnostics.Trace.WriteLine($"{beginDate} - {endDate} for {location.Value} in {shipID}");
                                var compareResult = DateTime.Compare(beginDate, endDate);

                                if (compareResult > 0)
                                {
                                    System.Diagnostics.Trace.WriteLine($"POSSIBLE DATE ERROR - { beginDate} may be greater than { endDate}   {shipID} - {location.Value} - {location.Attribute("location_guid").Value}");
                                }
                                else if (compareResult == 0)
                                {
                                    System.Diagnostics.Trace.WriteLine($"POSSIBLE DATE ERROR - { beginDate} is equal to { endDate}   {shipID} - {location.Value} - {location.Attribute("location_guid").Value}");
                                }
                            }
                           
                            {
                                SQLiteCommand insertCommand = new SQLiteCommand("insert into shipLocationDate (shipID, locationname, startdate, enddate, locationguid) values (@shipID, @locationname, @startdate, @enddate, @locationguid)", locationConnection);
                                insertCommand.Parameters.Add(new SQLiteParameter("@shipID", shipID));
                                insertCommand.Parameters.Add(new SQLiteParameter("@locationname", location.Value));
                                insertCommand.Parameters.Add(new SQLiteParameter("@startdate", beginDate == DateTime.MinValue ? string.Empty : beginDate.ToString()));
                                insertCommand.Parameters.Add(new SQLiteParameter("@enddate", endDate == DateTime.MinValue ? string.Empty : endDate.ToString()));
                                insertCommand.Parameters.Add(new SQLiteParameter("@locationguid", location.Attribute("location_guid").Value));

                                insertCommand.ExecuteNonQuery();
                            }

                        }
                        transaction.Commit();
                    }

                    shipCount++;
                }


                System.Diagnostics.Trace.WriteLine($"There are {uniqueLocations.Count} unique locations across {shipCount} ships", "INFO");


                //Create a SQLite 3 DB table and put all the locations into it. Look them up using the Google Maps API, try to get Lat / Long.
                //ONly allowed 2,500 per day, so get 2,500 and see what happens?

                var uniqueLocationsFileName = System.IO.Path.Combine(ROOT_PATH, "UniqueLocations.txt");

                System.IO.File.Delete(uniqueLocationsFileName);

                //Save out the uniqueLocations to a file so we can do reverse geo location separately.
                System.IO.File.AppendAllLines(uniqueLocationsFileName, uniqueLocations);
                                
                locationConnection.Close();
            }
        }

        private async Task GeocodeUniqueLocations()
        {
            var uniqueLocationsFileName = System.IO.Path.Combine(ROOT_PATH, "UniqueLocations.txt");

            var geocoder = new GoogleGeocoder();

            int count = 0;

            var uniqueLocations = System.IO.File.ReadAllLines(uniqueLocationsFileName);

            using (var locationConnection = OpenExistingLocationDatabase(false))
            {
                var totalUniqueLocations = uniqueLocations.Count();

                foreach (var location in uniqueLocations)
                {
                    //Stop after 10 just for debugging purposes.
                    /*if (count >= 10)
                        break;*/



                    await DoGeolocationLookup(geocoder, location, locationConnection);

                    var splitByComma = location.Split(',');

                    if (splitByComma.Length > 1)
                    {
                        foreach (var splitLocation in splitByComma)
                        {
                            await DoGeolocationLookup(geocoder, splitLocation.Trim(), locationConnection);
                        }
                    }

                    count++;

                    System.Diagnostics.Trace.WriteLine($"Processed {count} of {totalUniqueLocations}");
                }
            }
        }

        int totalDates = 0;
        string lastYear = string.Empty;

        private async Task DoGeolocationLookup(GoogleGeocoder geocoder, string location, SQLiteConnection locationConnection)
        {
            var shouldBeExcluded = this.invalidLocations.Any(o => string.Compare(o, location, true) == 0);
            if (!shouldBeExcluded)
            {
                //Now check the locationMarkers and skip those too.
                shouldBeExcluded = this.locationMarkers.Keys.Any(o => string.Compare(o, location, true) == 0);
                if (!shouldBeExcluded)
                {
                    //Check to see if the location already exists, if it does, then skip the whole next step, do not attempt to geocode.
                    using (SQLiteCommand sqlCommand = new SQLiteCommand("SELECT geocodeJSON from locationJSON where name = @name", locationConnection))
                    {
                        sqlCommand.Parameters.Add(new SQLiteParameter("@name", location));
                        var reader = sqlCommand.ExecuteReader();
                       
                        while (reader.Read())
                        {
                            var parsedJSON = geocoder.GetObjectFromJSON(reader["geocodeJSON"] as string);

                            if (parsedJSON.Status == "OVER_QUERY_LIMIT" &&
                                !string.IsNullOrEmpty(parsedJSON.ErrorMessage))
                            {
                                //Delete the entry from the table as well.
                                using (var deleteCommand = new SQLiteCommand("delete from locationJSON where name = @name", locationConnection))
                                {
                                    deleteCommand.Parameters.Add(new SQLiteParameter("@name", location));
                                    deleteCommand.ExecuteNonQuery();
                                    System.Diagnostics.Trace.WriteLine($"Repairing {location} due to bad reverse geo code request");
                                }

                                shouldBeExcluded = false;
                            }
                            else
                            {
                                //Skip reverse geocoding this. It is all OK.
                                shouldBeExcluded = true;
                            }
                        }
                        
                    }

                    if (!shouldBeExcluded)
                    {
                        try
                        {
                            var geocoderResultTuple = await geocoder.DoGeocodeRawJSON(location);

                            if (geocoderResultTuple.Item2.Status == "OVER_QUERY_LIMIT" &&
                                !string.IsNullOrEmpty(geocoderResultTuple.Item2.ErrorMessage))
                            {
                                throw new Exception("Toasty on requests for the day! Sorry!");
                            }

                            using (SQLiteCommand insertCommand = new SQLiteCommand("insert into locationJSON (name, geocodeJSON) values (@name, @geocodeJSON)", locationConnection))
                            {
                                insertCommand.Parameters.Add(new SQLiteParameter("@name", location));
                                insertCommand.Parameters.Add(new SQLiteParameter("@geocodeJSON", geocoderResultTuple.Item1));

                                insertCommand.ExecuteNonQuery();


                                System.Diagnostics.Trace.WriteLine($"Inserted new geocoding data for {location}");

                                //Wait some time between geocoder calls. Let's see how far we can get against the Google geocoder.
                                System.Threading.Thread.Sleep(500);
                            }
                        }
                        catch
                        {
                            //The geocoder broke. Let's log it but continue processing.
                            System.Diagnostics.Trace.WriteLine($"Exception while Reverse Geocoding {location} - Continuing");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine($"Skipping Reverse Geolocation lookup for '{location}'");
                    }
                }

            }
        }

        private void SetLastYear(string masterShipID, string year)
        {
            lastYear = year;
            //Go through the list of all dates with no year, and give them this year as an attribute.
            foreach (var noYearDateElement in noYearDateElements)
            {
                System.Diagnostics.Trace.WriteLine($"Repairing Year for {masterShipID} with {year}", "REPAIR");
                noYearDateElement.Add(new XAttribute("year", lastYear));
            }

            noYearDateElements.Clear();
        }

        List<XElement> noYearDateElements = new List<XElement>();

        private String getNotNullString(String inString)
        {
            if (string.IsNullOrEmpty(inString))
            {
                return string.Empty;
            }
            return inString;
        }

       double getProb(string tag, int wordIndex, CRFCliqueTree cliqueTree)
        {
            for (var iter = classifier.classIndex.iterator(); iter.hasNext();)
            {
                var label = iter.next();
                int labelIndex = classifier.classIndex.indexOf(label);
                double prob = cliqueTree.prob(wordIndex, labelIndex);
                if (label as string == tag)
                {
                    return prob;
                }

            }
            return 0.0;
        }

        /// <summary>
        /// This is a 1:1 port of the matching Java method from:
        /// https://github.com/stanfordnlp/CoreNLP/blob/f569983c8ad4e7890139b77775865cce1b82d4dc/src/edu/stanford/nlp/sequences/PlainTextDocumentReaderAndWriter.java#L418
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="sb"></param>
        /// <param name="cliqueTree"></param>
        private void printAnswersInlineXML(java.util.List doc, StringBuilder sb, CRFCliqueTree cliqueTree)
        {
            String background = "O";
            String prevTag = background;
            int wordCount = 0;
            for (var wordIter = doc.iterator(); wordIter.hasNext();)
            {
                var wi = wordIter.next() as CoreLabel;
                //(classifyList.iterator().next() as CoreLabel).get(new CoreAnnotations.AnswerAnnotation().getClass())
                String tag = getNotNullString(wi.get(new CoreAnnotations.AnswerAnnotation().getClass()) as String);

                String before = getNotNullString(wi.get(new CoreAnnotations.BeforeAnnotation().getClass()) as String);

                String current = getNotNullString(wi.get(new CoreAnnotations.OriginalTextAnnotation().getClass()) as String);
                if (tag != prevTag)
                {
                    if (prevTag != background && tag != background)
                    {
                        sb.print("</");
                        sb.print(prevTag);
                        sb.print('>');

                        sb.print(before);

                        sb.print('<');
                        sb.print(tag);
                        sb.print(" PROBABILITY=\"");
                        sb.print(getProb(tag, wordCount, cliqueTree));
                        sb.print("\" ");
                        sb.print('>');
                    }
                    else if (prevTag != background)
                    {
                        sb.print("</");
                        sb.print(prevTag);
                        sb.print('>');
                        sb.print(before);
                    }
                    else if (tag != background)
                    {
                        sb.print(before);
                        sb.print('<');
                        sb.print(tag);
                        sb.print(" PROBABILITY=\"");
                        sb.print(getProb(tag, wordCount, cliqueTree));
                        sb.print("\" ");

                        sb.print('>');
                    }
                }
                else
                {
                    sb.print(before);
                }
                sb.print(current);
                String afterWS = getNotNullString(wi.get(new CoreAnnotations.AfterAnnotation().getClass()) as String);

                if (tag != background && !wordIter.hasNext())
                {
                    sb.print("</");
                    sb.print(tag);
                    sb.print('>');
                    prevTag = background;
                }
                else
                {
                    prevTag = tag;
                }
                sb.print(afterWS);

                wordCount++;
            }
        }


        void ProcessPersonLocationOrganization(string masterShipID, XElement element, XElement destinationElement)
        {

            foreach (var node in element.Nodes())
            {
                if (node is XText)
                {
                    var textValue = (node as XText).Value;
                    textValue = textValue.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;");
                
                    //raby has a Midway, Ga. in it which we want to preserve.
                    if (!masterShipID.StartsWith("raby"))
                    {
                        textValue = textValue.Replace("Midway Island", "Midway").Replace("Midway", "Midway Island");
                    }

                    var sentences = classifier.classify(textValue);
                    
                    var sb = new StringBuilder();
                    for (var itr = sentences.iterator(); itr.hasNext();)
                    {                       
                        var sentence = itr.next() as java.util.List;
                        var cliqueTree = classifier.getCliqueTree(sentence);
                        printAnswersInlineXML(sentence , sb, cliqueTree);
                    }

                    var classifierResult = sb.ToString();


                    var settings = new XmlReaderSettings
                    {
                        ConformanceLevel = ConformanceLevel.Fragment,
                        IgnoreWhitespace = true
                    };

                    using (var stringReader = new StringReader(classifierResult))
                    using (var xmlReader = XmlReader.Create(stringReader, settings))
                    {
                        xmlReader.MoveToContent();
                        while (xmlReader.ReadState != ReadState.EndOfFile)
                        {
                            destinationElement.Add(XNode.ReadFrom(xmlReader));
                        }
                    }

                    //Remove any LOCATION, PERSON, ORGANIZATION tags that are contained within a "i".
                    var invalidPOLData = destinationElement.Elements().Where(e => (e.Name == "LOCATION" || e.Name == "ORGANIZATION" || e.Name == "PERSON") && string.Compare(e.Parent.Name.LocalName, "i", true) == 0);

                    foreach (var invalidPOL in invalidPOLData)
                    {
                        //Log the tag we are removing.
                        System.Diagnostics.Trace.WriteLine($"Removing POL: {invalidPOL.Parent}", "REPAIR");

                        //Add all child nodes of the invalid POL tag to the parent.
                        var afterNode = invalidPOL.NodesAfterSelf().FirstOrDefault();
                        var beforeNode = invalidPOL.NodesBeforeSelf().LastOrDefault();
                        if (afterNode != null/* && beforeNode != null*/)
                        {
                            afterNode.AddBeforeSelf(invalidPOL.Nodes().ToArray());
                        }
                        else if (beforeNode != null)
                        {
                            beforeNode.AddAfterSelf(invalidPOL.Nodes().ToArray());
                        }
                        else
                        {
                            invalidPOL.Parent.Add(invalidPOL.Nodes().ToArray());
                        }
                        //Add it this way otherwise formatting issues occur within the <i> tags.

                        //Now safely remove the P,O,L tag with the contents safely copied.
                        invalidPOL.Remove();
                    }

                  

                    //Now that we have removed the tag, let's also associate any ship date to what may be in the "i" tag.

                    var iTags = destinationElement.Elements().Where(e => string.Compare(e.Name.LocalName, "i", true) == 0);
                    foreach (var iTag in iTags)
                    {

                        if (iTag.Attributes("ship-registry").Count() != 0)
                        {
                            continue;
                        }

                        //Check for a text node right after the tag that may contain a ship designation i.e. ()
                        var followingTextNode = iTag.NodesAfterSelf().FirstOrDefault();
                        if (followingTextNode != null && followingTextNode is XText)
                        {
                            var sourceElementText = (followingTextNode as XText).Value.Trim();
                            var shipRegistryMatch = shipRegistryRegexLoose.Match(sourceElementText);
                            if (shipRegistryMatch.Success && shipRegistryMatch.Index == 0)
                            {
                                iTag.Add(new XAttribute("ship-registry", "normalized"));
                                iTag.Add(new XAttribute("ship-designation", shipRegistryMatch.Value));
                            }
                            else
                            {
                                var alternateShipRegistryMatch = shipRegistryAlternateRegexLoose.Match(sourceElementText);
                                if (alternateShipRegistryMatch.Success && alternateShipRegistryMatch.Index == 0)
                                {
                                    iTag.Add(new XAttribute("ship-registry", "notnormalized"));
                                    iTag.Add(new XAttribute("ship-designation", alternateShipRegistryMatch.Value));
                                }
                            }
                        }
                    }

                    //Search through the destination element and aggregate any LOCATION tags that are siblings.
                    List<XNode> locationElementsToRemove = new List<XNode>();

                    var locationElements = destinationElement.Elements("LOCATION").ToArray();

                    for(int locationElementIndex = 0; locationElementIndex < locationElements.Length; locationElementIndex++)
                    {
                        //We are only looking to repair the following scenario:
                        /*
                        
                        <LOCATION>Ditchly</LOCATION>
                        ,
                        <LOCATION>Va</LOCATION>

                         */
                        var possibleLocationElement = locationElements[locationElementIndex];
                        {
                            //Check the next sibling nodes, if they are a text node and another LOCATION element, then aggregate the text node and LOCATION element together,
                            //and remove the text node + extra location element from the DOM.

                            //Next 2 nodes:

                            if (possibleLocationElement.NodesAfterSelf() != null && possibleLocationElement.NodesAfterSelf().Count() >= 2)
                            {
                                var textNode = possibleLocationElement.NodesAfterSelf().First() as XText;
                                if (textNode != null)
                                {
                                    var otherLocationElement = textNode.NodesAfterSelf().First() as XElement;

                                    if (textNode != null && otherLocationElement != null && otherLocationElement.Name == "LOCATION")
                                    {
                                        //Do not aggregate the locations if they are separated by a ;, or by a text node with anything other than
                                        //Punctuation in it.
                                        if (textNode.Value.Trim().Where(c => !char.IsPunctuation(c) || c == ';').Count() == 0)
                                        {
                                            possibleLocationElement.Value += textNode.Value + otherLocationElement.Value;

                                            System.Diagnostics.Trace.WriteLine($"Aggregated Locations in {masterShipID}: {possibleLocationElement.Value}", "REPAIR");

                                            //Skip the next location element...
                                            locationElementIndex++;

                                            //Eradicate the text node and the element.
                                            textNode.Remove();
                                            otherLocationElement.Remove();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //All locations have been aggregated. Time to 'GUID' them up so we can link to them within the document later.
                    foreach (var locationElement in destinationElement.Elements("LOCATION"))
                    {
                        if (locationElement.Attribute("location_guid") == null)
                        {
                            locationElement.Add(new XAttribute("location_guid", Guid.NewGuid().ToString()));
                        }
                    }
                }
                else if (node is XElement)
                {
                    var sourceElement = (node as XElement);
                    XElement newDestinationElement = new XElement(sourceElement.Name);
                    foreach (var attribute in sourceElement.Attributes())
                    {
                        newDestinationElement.Add(attribute);
                    }
                    destinationElement.Add(newDestinationElement);
                    ProcessPersonLocationOrganization(masterShipID, node as XElement, newDestinationElement);
                }
            }
        }

        class ProcessElementContext
        {
            public bool FirstAlternateShipHistoryDiscovered { get; set; }
            public string Title { get; internal set; }
        }

        void ProcessElement(string masterShipID, XElement element, XElement destinationElement, ProcessElementContext context)
        {
            

            foreach (var node in element.Nodes())
            {
                if (node is XText)
                {
                    var textValue = (node as XText).Value;

                    DoTextDateProcessing(masterShipID, destinationElement, node, textValue);
                }
                else if (node is XElement)
                {
                    var sourceElement = (node as XElement);

                    //Copy all the attributes from the existing element.
                    XElement newDestinationElement = new XElement(sourceElement.Name);
                    foreach (var attribute in sourceElement.Attributes())
                    {
                        newDestinationElement.Add(attribute);
                    }



                    //Check the source element and see if it is a ship stats element. If it is, mark it because we will want to parse it, and use it as a marker to decide
                    //where ships history begins.
                    //The stats element contains a child that starts with (<CHARS>-<NUMBER>:
                    if (shipRegistryElement == null)
                    {
                        var sourceElementText = sourceElement.Value;
                        var shipRegistryMatch = shipRegistryRegex.Match(sourceElementText);
                        if (shipRegistryMatch.Success && shipRegistryMatch.Index == 0)
                        {
                            newDestinationElement.Add(new XAttribute("possible-history-start", "true"));
                            newDestinationElement.Add(new XAttribute("ship-registry", "normalized"));

                            AddMarkupShipMetrics(masterShipID, sourceElement, newDestinationElement);

                            shipRegistryElement = newDestinationElement;
                        }
                        else
                        {
                            var alternateShipRegistryMatch = shipRegistryAlternateRegex.Match(sourceElementText);
                            if (alternateShipRegistryMatch.Success && alternateShipRegistryMatch.Index == 0)
                            {
                                newDestinationElement.Add(new XAttribute("possible-history-start", "true"));
                                newDestinationElement.Add(new XAttribute("ship-registry", "notnormalized"));

                                AddMarkupShipMetrics(masterShipID, sourceElement, newDestinationElement);

                                shipRegistryElement = newDestinationElement;
                               
                            }
                        }
                        
                    }

                    if (!context.FirstAlternateShipHistoryDiscovered)
                    {
                        AlternateMarkIfShipHistoryBegin(sourceElement, newDestinationElement, context);
                    }


                    destinationElement.Add(newDestinationElement);
                    ProcessElement(masterShipID, node as XElement, newDestinationElement, context);
                }
            }
        }

        private void AddMarkupShipMetrics(string masterShipID, XElement sourceElement, XElement newDestinationElement)
        {
            //Samples: 
            /*
             (DDG-1000: displacement 14,564; length 610'; beam 80.7'; draft 28'; speed 30 knots; complement 148; armament 20 Mk 57 Vertical Launch System modules (80 cells) for RIM-162 Evolved Sea Sparrow Missiles, BGM-109E Tactical <i>Tomahawk</i>s, and <ORGANIZATION PROBABILITY="0.486440705968583">RUM-139C Anti-Submarine Rockets</ORGANIZATION>, two 155 millimeter Advanced Gun Systems, two Mk 46 30 millimeter <ORGANIZATION PROBABILITY="0.709120851876277">Naval Weapon Systems</ORGANIZATION>, and two Sikorsky MH-60R <i>Seahawk</i> helicopters or one <i>Seahawk</i> and up to three <ORGANIZATION PROBABILITY="0.981090465287876">Northrop Grumman</ORGANIZATION> RQ-8A <i>Fire Scout</i> Vertical Takeoff and Landing Tactical Unmanned Aerial Vehicles; class <i>Zumwalt</i>)
            (AT-95: displacement 1,589 (trial); length 205'0"; beam 38'6"; draft 15'4" (full load); speed 16.5 knots (trial); complement 85; armament 1 3-inch, 2 40 millimeter; class <i>Navajo</i>)
             */

            var dataElement = sourceElement.Element("p");

            if (dataElement == null)
            {
                dataElement = sourceElement;
            }

            var rawText = dataElement.Value;

            var shipMetricsElement = new XElement("shipmetrics");

            var rawSplit = rawText.Split(';');
            
            foreach (var rawValue in rawSplit)
            {
                var metricElement = new XElement("metric");

                var trimmedRawValue = rawValue.Trim();

                string shipValueKey = string.Empty;
                string shipValue = string.Empty;

                var spaceSeparatedValues = trimmedRawValue.Split(' ');
                if (rawValue.StartsWith("("))
                {                   
                    //Then we need to look after the : for the ship key.               
                    int counter = 0;
                    int indexOfColon = int.MinValue;
                    foreach (var possibleValue in spaceSeparatedValues)
                    {
                        if (possibleValue.EndsWith(":"))
                        {
                            indexOfColon = counter;
                            break;
                        }
                        counter++;
                    }

                    if (indexOfColon != int.MinValue)
                    {
                        if (spaceSeparatedValues.Length >= indexOfColon + 2)
                        {
                            shipValueKey = spaceSeparatedValues.Skip(indexOfColon + 1).First();
                            shipValue = string.Join(" ", spaceSeparatedValues.Skip(indexOfColon + 2).ToArray());
                            System.Diagnostics.Trace.WriteLine($"{masterShipID} - Ship Metrics: {shipValueKey} = {shipValue}");
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine($"{masterShipID} -Not sure we are processing ship metric data");
                            shipValue = "UNKNOWN";
                            shipValueKey = "UNKNOWN";
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine($"{masterShipID} -First entry in ship data has nothing that ends with a colon");
                        shipValue = "UNKNOWN";
                        shipValueKey = "UNKNOWN";
                    }
                }
                else
                {
                    shipValueKey = spaceSeparatedValues[0];
                    shipValue = string.Join(" ", spaceSeparatedValues.Skip(1).ToArray());
                }

                var valueKeyElement = new XElement("key");
                valueKeyElement.Value = shipValueKey;

                var valueElement = new XElement("value");
                valueElement.Value = shipValue;

                metricElement.Add(valueKeyElement);
                metricElement.Add(valueElement);

                shipMetricsElement.Add(metricElement);
            }

            newDestinationElement.Add(shipMetricsElement);
        }

        private bool AlternateMarkIfShipHistoryBegin(XElement sourceElement, XElement newDestinationElement, ProcessElementContext context)
        {
            if (sourceElement.Name == "p" && !context.FirstAlternateShipHistoryDiscovered)
            {
                var paragraphText = sourceElement.Value.ToLowerInvariant();
             
                if (sourceElement.Value.ToLowerInvariant().Contains("cl."))
                {
                    //This is the top ship registry text, we don't want to scan this to see if it is the history begin.
                    return false;
                }
            
                var iElements = sourceElement.Descendants("i");
                foreach (var iElement in iElements)
                {
                    var splitName = context.Title.Split(' ');
                    var stringToFind = splitName[0].ToLowerInvariant().Trim();
                    if (iElement != null && iElement.Value.ToLowerInvariant().Contains(stringToFind))
                    {
                        newDestinationElement.Add(new XAttribute("possible_start_ship_log", "true"));
                        context.FirstAlternateShipHistoryDiscovered = true;
                        return true;
                    }
                }
                
                {
                    if (paragraphText.Contains("was laid down"))
                    {
                        newDestinationElement.Add(new XAttribute("possible_start_ship_log", "true"));
                        context.FirstAlternateShipHistoryDiscovered = true;
                        return true;
                    }
                }
            }

            return false;
        }

        private void DoTextDateProcessing(string masterShipID, XElement destinationElement, XNode node, string textValue)
        {
            var matchedDates = dateRegex.Matches(textValue);


            if (matchedDates != null && matchedDates.Count > 0)
            {
                var previousMatch = 0;


                foreach (Match match in matchedDates)
                {
                    totalDates++;

                    //First add the string before the matched date to the paragraph XML.
                    destinationElement.Add(new XText(textValue.Substring(previousMatch, match.Index - previousMatch)));

                    //Now add the parsed date as a new XElement to the above paragraph.

                    XElement dateElement = new XElement("date");
                    dateElement.Add(new XText(match.Value));

                    var preprocessedMatches = match.Groups.Cast<Group>().Where(o => !string.IsNullOrEmpty(o.Value.Trim())).ToArray();

                    if (preprocessedMatches.Length == 5)
                    {
                        //2 options here:
                        //one is 21 May 1901 -- Which means that the preprocessedMatches[2] is an integer.
                        //Other is a seasonal date fall of 1867
                        int day = Int32.MinValue;
                        if (Int32.TryParse(preprocessedMatches[2].Value.Trim(), out day))
                        {


                            //Group 1 == Day in Month.
                            //Group 2 == Month value.
                            //Group 3 == Year value.
                            SetLastYear(masterShipID, preprocessedMatches[4].Value.Trim());
                            dateElement.Add(new XAttribute("year", preprocessedMatches[4].Value.Trim()));
                            dateElement.Add(new XAttribute("month", preprocessedMatches[3].Value.Trim()));
                            dateElement.Add(new XAttribute("day", preprocessedMatches[2].Value.Trim()));
                        }
                        else
                        {
                            SetLastYear(masterShipID, preprocessedMatches[4].Value.Trim());
                            dateElement.Add(new XAttribute("year", lastYear));
                            dateElement.Add(new XAttribute("season", preprocessedMatches[2].Value.Trim()));
                            //TODO: Process season into a default month.
                        }
                    }
                    else if (preprocessedMatches.Length == 4)
                    {
                        if (preprocessedMatches[2].Value.Trim() == "in")
                        {
                            SetLastYear(masterShipID, preprocessedMatches[3].Value.Trim());
                            dateElement.Add(new XAttribute("year", lastYear));
                        }
                        else
                        {
                            //This could have the value 'in 1600' or 'June 1865' or '12 August'
                            //Check to see if Groups[1] is an integer. If it is, then write out day / month.
                            int possibleDay = Int32.MinValue;
                            if (Int32.TryParse(preprocessedMatches[2].Value.Trim(), out possibleDay))
                            {
                                dateElement.Add(new XAttribute("month", preprocessedMatches[3].Value.Trim()));
                                dateElement.Add(new XAttribute("day", preprocessedMatches[2].Value.Trim()));
                                if (!string.IsNullOrEmpty(lastYear))
                                {
                                    dateElement.Add(new XAttribute("year", lastYear));
                                }
                                else
                                {
                                    noYearDateElements.Add(dateElement);
                                    System.Diagnostics.Trace.WriteLine($"No year present for partial date - {masterShipID}", "INFO");
                                    dateElement.Add(new XAttribute("invalid-year-value", "true"));
                                }
                            }
                            else
                            {
                                SetLastYear(masterShipID, preprocessedMatches[3].Value.Trim());
                                //Otherwise write out month / year
                                dateElement.Add(new XAttribute("year", preprocessedMatches[3].Value.Trim()));
                                dateElement.Add(new XAttribute("month", preprocessedMatches[2].Value.Trim()));
                            }
                        }
                    }
                    else if (preprocessedMatches.Length == 3)
                    {
                        //If value is not an integer, then it is a month.
                        int possibleYear = Int32.MinValue;
                        if (!Int32.TryParse(preprocessedMatches[2].Value.Trim(), out possibleYear))
                        {
                            dateElement.Add(new XAttribute("month", preprocessedMatches[2].Value.Trim()));

                            if (!string.IsNullOrEmpty(lastYear))
                            {
                                dateElement.Add(new XAttribute("year", lastYear));
                            }
                            else
                            {
                                noYearDateElements.Add(dateElement);
                                System.Diagnostics.Trace.WriteLine($"No year present for partial date - {masterShipID}", "INFO");
                                dateElement.Add(new XAttribute("invalid-year-value", "true"));
                            }
                        }
                        else
                        {
                            //Is invalid.
                            System.Diagnostics.Trace.WriteLine($"Date Is Invalid - {masterShipID}", "INFO");
                            dateElement.Add(new XAttribute("invalid-one-value", "true"));
                        }
                    }
                    else
                    {
                        //Is invalid.
                        System.Diagnostics.Trace.WriteLine($"Is Invalid - 2 - {masterShipID}", "INFO");
                        dateElement.Add(new XAttribute("invalid", "true"));
                    }

                    //TODO: Put the broken out date parts into the XDocument.
                    destinationElement.Add(dateElement);

                    previousMatch = match.Index + match.Length;
                }

                //Add any trailing text from the end of the last match....
                destinationElement.Add(new XText(textValue.Substring(previousMatch, textValue.Length - previousMatch)));
            }
            else
            {
                destinationElement.Add(node);
            }
        }

        XElement shipRegistryElement = null;

        Regex shipRegistryAlternateRegex = new Regex(@"\(.*\:", RegexOptions.Compiled);

        Regex shipRegistryRegex = new System.Text.RegularExpressions.Regex(@"\([A-Z]*\-[0-9]*\:", RegexOptions.Compiled);


        Regex shipRegistryAlternateRegexLoose = new Regex(@"\(.*\)", RegexOptions.Compiled);

        Regex shipRegistryRegexLoose = new System.Text.RegularExpressions.Regex(@"\([A-Z]*\-[0-9]*\)", RegexOptions.Compiled);
        //Regex dateRegex = new Regex(@"(\b\d{1,2}\D{0,3})?\b(January|February|March|April|May|June|July|August|September|October|November|December)(\s\d{0,4})?", RegexOptions.Compiled);

        //Crazy regex that picks up more dates
        Regex dateRegex = new Regex(@"(\b(fall|winter|spring|summer)\b(\sof)(\s[0-9]{4}))|(\b(in)\s([0-9]{4}))|((\b\d{1,2}\D{0,3})?(\bJanuary|February|March|April|May|June|July|August|September|October|November|December)\b(\s\d{0,4})?)", RegexOptions.Compiled);

        //\b(of|in)\s([0-9]{4})

        private void MatchDates()
        {
            //Regex that seems most effective for date extraction:
            // /(\b\d{1,2}\D{0,3})?\b(January|February|March|April|May|June|July|August|September|October|November|December)(\s\d{0,4})?/g

        }
    }

    public static class StringBuilderExtension
    {
        public static void print(this StringBuilder sb, string stringToWrite)
        {
            sb.Append(stringToWrite);
        }

        public static void print(this StringBuilder sb, char charToWrite)
        {
            sb.Append(charToWrite);
        }

        public static void print(this StringBuilder sb, double doubleToWrite)
        {
            sb.Append(doubleToWrite);
        }
    }
}
