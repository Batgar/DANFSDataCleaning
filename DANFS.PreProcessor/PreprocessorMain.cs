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

namespace DANFS.PreProcessor
{
    class PreprocessorMain
    {
        CRFClassifier classifier;

        public void Process()
        {
            MakeLocationDictionary();
            return;

            //TryGetRanksOfPeople();
            //return;


            var pathToMainDANFSDatabase = @"C:\Users\Batgar\Documents\danfs.sqlite3";
            var connection = new SQLiteConnection(string.Format("Data Source={0};Version=3", pathToMainDANFSDatabase));
            connection.Open();
            var command = new SQLiteCommand("select * from danfs_ships", connection);
            var manifestReader = command.ExecuteReader();

            int totalShipCount = 0;
            int missingShipRegistries = 0;
            totalDates = 0;

            // Path to the folder with classifiers models
            var jarRoot = @"C:\Users\Batgar\Downloads\stanford-ner-2015-12-09\stanford-ner-2015-12-09";
            var classifiersDirecrory = System.IO.Path.Combine(jarRoot, @"classifiers");

            // Loading 3 class classifier model
            classifier = CRFClassifier.getClassifierNoExceptions(
                classifiersDirecrory + @"\english.all.3class.distsim.crf.ser.gz");

            //Generates a JSON manifest
            List<ShipManifestEntry> shipManifestEntries = new List<ShipManifestEntry>();
            while (manifestReader.Read())
            {
                shipManifestEntries.Add(new ShipManifestEntry()
                {
                    ID = manifestReader["id"] as string,
                    Title = manifestReader["title"] as string
                });
            }
            //Serialize all to a JSON file.

            string manifestJSONPath = @"C:\Users\Batgar\Documents\Ships\manifest.json";

            if (File.Exists(manifestJSONPath))
            {
                File.Delete(manifestJSONPath);
            }

            System.IO.File.WriteAllText(manifestJSONPath, JsonConvert.SerializeObject(shipManifestEntries));

            manifestReader.Close();

            manifestReader = null;

            var reader = command.ExecuteReader();

            //Generates all files and inserts dates.
            while (reader.Read())
            {
                try
                {
                    var doc = XDocument.Parse("<root>" + (string)reader["history"] + "</root>");
                    //Console.WriteLine("Ship {0} is valid XML", reader["title"]);

                    var processedValue = doc.Root.Attribute("date");

                    if (processedValue != null && processedValue.Value == "true")
                    {
                        continue;
                    }

                    XElement rootElement = new XElement("root");

                    lastYear = string.Empty;

                    shipRegistryElement = null;

                    noYearDateElements.Clear();

                    ProcessElement(reader["id"] as String, doc.Root, rootElement);

                    var augmentedDoc = new XDocument();
                    augmentedDoc.Add(rootElement);

                    if (shipRegistryElement == null)
                    {
                        Console.WriteLine("No ship registry for {0}", reader["id"]);
                        missingShipRegistries++;
                    }

                    //Throw in our flag so we don't double process!
                    rootElement.Add(new XAttribute("date", "true"));

                    augmentedDoc.Save(string.Format(@"C:\Users\Batgar\Documents\Ships\{0}.xml", reader["id"]));
                    totalShipCount++;

                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while processing ship {0} - {1}", reader["id"], e.Message);
                }
            }

            //Now pass through it a second time, this time we are going to put in Person, Location, Organization information.
            foreach (var file in Directory.GetFiles(@"C:\Users\Batgar\Documents\Ships", "*.xml"))
            {
                var doc = XDocument.Load(file);

                XElement rootElement = new XElement("root");

                var processedValue = doc.Root.Attribute("personLocationOrganization");

                if (processedValue != null && processedValue.Value == "true")
                {
                    continue;
                }

                ProcessPersonLocationOrganization(Path.GetFileNameWithoutExtension(file), doc.Root, rootElement);

                //Throw in our flag so we don't double process!
                rootElement.Add(new XAttribute("personLocationOrganization", "true"));



                var augmentedDoc = new XDocument();
                augmentedDoc.Add(rootElement);
                augmentedDoc.Save(string.Format(@"C:\Users\Batgar\Documents\Ships\{0}.xml", Path.GetFileNameWithoutExtension(file)));
            }

            Console.WriteLine("Total Ships: {0} -- Missing Registries: {1} - Total Dates Logged: {2}", totalShipCount, missingShipRegistries, totalDates);



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


        private void TryGetRanksOfPeople()
        {
            List<string> possibleRanks = new List<string>();

            int shipCount = 0;

            //Now we will produce a location dictionary from all locations, and dump the JSON.
            foreach (var file in Directory.GetFiles(@"C:\Users\Batgar\Documents\Ships", "*.xml"))
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

            File.WriteAllLines(@"C:\Users\Batgar\Documents\ShipsStats\AllPossibleRanks.txt", possibleRanks.ToArray());
        }

        private async void MakeLocationDictionary()
        {

            List<string> uniqueLocations = new List<string>();

            int shipCount = 0;

            //Now we will produce a location dictionary from all locations, and dump the JSON.
            foreach (var file in Directory.GetFiles(@"C:\Users\Batgar\Documents\Ships", "*.xml"))
            {
                var doc = XDocument.Load(file);

                //TODO: Only do this for now to get the master location list, skip anything in an italics node due to ship name formatting (i.e. New Jersey - Battleship)
                var locations = doc.Root.Descendants("LOCATION").Where(e => e.Parent.Name != "i" && !uniqueLocations.Contains(e.Value)).Select(e => e.Value).Distinct();

                uniqueLocations.AddRange(locations);


                /*var shipID = Path.GetFileNameWithoutExtension(file);

                foreach (var location in locations)
                {
                    var closestPreviousDateElement = location.ElementsBeforeSelf("date")?.FirstOrDefault();
                    var closestNextDateElement = location.ElementsAfterSelf("date")?.FirstOrDefault();

                    //We have a location, a ship ID, and possible associated dates.


                }*/

                shipCount++;
            }


           

            //Create a SQLite 3 DB table and put all the locations into it. Look them up using the Google Maps API, try to get Lat / Long.
            //ONly allowed 2,500 per day, so get 2,500 and see what happens?

            var locationDatabasePath = @"C:\Users\Batgar.Documents\shiplocations.sqlite";

            File.Delete(locationDatabasePath);

            SQLiteConnection.CreateFile(locationDatabasePath);

            var locationConnection = new SQLiteConnection("Data Source=MyDatabase.sqlite;Version=3;");
            locationConnection.Open();

            string createTableSql = "create table locations (name text, formattedAddress text, latitude real, longitude real)";

            SQLiteCommand createTableCommand = new SQLiteCommand(createTableSql, locationConnection);
            createTableCommand.ExecuteNonQuery();


            string sql = "insert into locations (name, formattedAddress, latitude, longitude) values ({0}, {1}, {2}, {3})";

            var insertCommand = new SQLiteCommand(sql, locationConnection);
            insertCommand.ExecuteNonQuery();

            

        

            locationConnection.Close();


            Console.WriteLine("There are {0} unique locations across {1} ships", uniqueLocations.Count, shipCount);
        }

        int totalDates = 0;
        string lastYear = string.Empty;

        private void SetLastYear(string masterShipID, string year)
        {
            lastYear = year;
            //Go through the list of all dates with no year, and give them this year as an attribute.
            foreach (var noYearDateElement in noYearDateElements)
            {
                Console.WriteLine("Repairing Year for {0} with {1}", masterShipID, year);
                noYearDateElement.Add(new XAttribute("year", lastYear));
            }

            noYearDateElements.Clear();
        }

        List<XElement> noYearDateElements = new List<XElement>();

        void ProcessPersonLocationOrganization(string masterShipID, XElement element, XElement destinationElement)
        {
            foreach (var node in element.Nodes())
            {
                if (node is XText)
                {
                    var textValue = (node as XText).Value;
                    textValue = textValue.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;");
                    
                    var classifierResult = classifier.classifyWithInlineXML(textValue);
                    //Try to get locations out of it, just print them out for now....
                    //Console.WriteLine("{0}\n", classifierResult);
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
                        Console.WriteLine("Removing POL: {0}", invalidPOL.Parent);

                        //Add all child nodes of the invalid POL tag to the parent.
                        invalidPOL.Parent.Add(invalidPOL.Nodes().ToArray());
                                               
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

                                            Console.WriteLine("Aggregated Locations in {0}: {1}", masterShipID, possibleLocationElement.Value);

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

                    //Cataloging alternate LOCATION types that we don't want to map, but we don't want to lose either.
                    var locationMarkers = new Dictionary<string, string>()
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

                    var invalidLocations = new string[] {  "United States Navy", "U.S.S" };
                       
                    

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

        void ProcessElement(string masterShipID, XElement element, XElement destinationElement)
        {
            

            foreach (var node in element.Nodes())
            {
                if (node is XText)
                {
                    var textValue = (node as XText).Value;

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
                                            Console.WriteLine("2 - No year present for partial date - {0}", masterShipID);
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
                                        Console.WriteLine("1- No year present for partial date - {0}", masterShipID);
                                        dateElement.Add(new XAttribute("invalid-year-value", "true"));
                                    }
                                }
                                else
                                {
                                    //Is invalid.
                                    Console.WriteLine("Is Invalid - 1 - {0}", masterShipID);
                                    dateElement.Add(new XAttribute("invalid-one-value", "true"));
                                }
                            }
                            else
                            {
                                //Is invalid.
                                Console.WriteLine("Is Invalid - 2 - {0}", masterShipID);
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
                else if (node is XElement)
                {
                    var sourceElement = (node as XElement);
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
                            shipRegistryElement = newDestinationElement;
                        }
                        else
                        {
                            var alternateShipRegistryMatch = shipRegistryAlternateRegex.Match(sourceElementText);
                            if (alternateShipRegistryMatch.Success && alternateShipRegistryMatch.Index == 0)
                            {
                                newDestinationElement.Add(new XAttribute("possible-history-start", "true"));
                                newDestinationElement.Add(new XAttribute("ship-registry", "notnormalized"));
                                shipRegistryElement = newDestinationElement;
                            }
                        }
                        
                    }

                    destinationElement.Add(newDestinationElement);
                    ProcessElement(masterShipID, node as XElement, newDestinationElement);
                }
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
}
