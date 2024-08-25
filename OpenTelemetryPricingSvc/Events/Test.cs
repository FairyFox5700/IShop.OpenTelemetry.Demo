namespace OpenTelemetryPricingSvc.Events
{
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Collections;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text.RegularExpressions;
    using System.Text;
    using System;



    class Result
    {

        /*
         * Complete the 'solution' function below.
         *
         * The function is expected to return a STRING.
         * The function accepts following parameters:
         *  1. STRING_ARRAY events
         *  2. STRING_ARRAY drugInfos
         */


        // would be nice to use a strtegy pattern
        public static string solution(List<string> events, List<string> drugInfos)
        {

            var days = new Dictionary<int, (double bsa, Dictionary<string, int> drugs)>();

            var lifetimeDoseByDrug = new Dictionary<string, int>();
            var dailyDoseMultiplierByDrug = new Dictionary<string, int>();
            var currentDay = -1;
            foreach (var line in events)
            {
                var parts = line.Split(", ");

                if (parts[0] == "Day")
                {
                    currentDay = int.Parse(parts[1]);
                    days[currentDay] = new(0, new Dictionary<string, int>());
                }

                else if (parts[0] == "BSA")
                {
                    days[currentDay] = new(double.Parse(parts[1]), new Dictionary<string, int>());
                }
                else
                {
                    var drugName = parts[0];

                    var dose = int.Parse(parts[1]);

                    if (!days[currentDay].drugs.ContainsKey(drugName))
                    {
                        days[currentDay].drugs[drugName] = 0;
                    }

                    days[currentDay].drugs[drugName] += dose;
                }
            }

            int numberOfInteractions = drugInfos.Count();


            var interactions = new Dictionary<string, HashSet<string>>();

            for (int i = 1; i < numberOfInteractions; i++)
            {
                var parts = drugInfos[i].Split(", ");

                var drugName = parts[0];
                var drugs = parts.Skip(3).ToHashSet();

                var dailyDoseMultiplier = int.Parse(parts[1]);
                var maxLifetimeDose = int.Parse(parts[2]);
                if (!interactions.ContainsKey(drugName))
                {
                    interactions.Add(drugName, new HashSet<string>());
                }
                interactions[drugName] = drugs;

                if (!lifetimeDoseByDrug.ContainsKey(drugName))
                {
                    lifetimeDoseByDrug.Add(drugName, maxLifetimeDose);
                }

                if (!dailyDoseMultiplierByDrug.ContainsKey(drugName))
                {
                    dailyDoseMultiplierByDrug.Add(drugName, maxLifetimeDose);
                }

            }


            var cumulativeDoseByDrug = new Dictionary<string, int>();

            foreach (var (dayNumber, (bsa, drugs)) in days)
            {
                var collectedInteractions = new List<(string, string)>();

                foreach (var (drugName, dose) in drugs)
                {
                    // accumulate doses
                    if (!cumulativeDoseByDrug.ContainsKey(drugName))
                    {
                        cumulativeDoseByDrug.Add(drugName, 0);
                    }

                    cumulativeDoseByDrug[drugName] += dose;

                    if (interactions.ContainsKey(drugName))
                    {
                        foreach (var interaction in interactions[drugName])
                        {
                            if (drugName.CompareTo(interaction) > 0 && drugs.ContainsKey(interaction))
                            {
                                collectedInteractions.Add((interaction, drugName));
                            }
                            else
                            {
                                collectedInteractions.Add((drugName, interaction));
                            }
                        }
                    }


                    if (collectedInteractions.Count() > 0)
                    {
                        var firstInteraction = collectedInteractions
                            .OrderBy(e => e.Item1)

                            .First();

                        return $"Day {dayNumber}: {firstInteraction.Item1} interacts with {firstInteraction.Item2}";
                    }

                }

                // check daily dose level
                foreach (var (drugName, dose) in drugs)
                {
                    if (dailyDoseMultiplierByDrug.ContainsKey(drugName) && dailyDoseMultiplierByDrug[drugName] * bsa >= dose)
                    {
                        return $"Day {dayNumber}: Daily dose for {drugName} exceeded";
                    }


                }


                // chech life time doses violations
                foreach (var (drug, limit) in lifetimeDoseByDrug)
                {
                    if (cumulativeDoseByDrug.ContainsKey(drug) && cumulativeDoseByDrug[drug] >= limit)
                    {
                        return $"Day {dayNumber}: Life time dose for {drug} exceeded";
                    }
                }
            }

            return "";


        }

    }

    class Solution
    {
        public static void Main(string[] args)
        {
            TextWriter textWriter = new StreamWriter(@System.Environment.GetEnvironmentVariable("OUTPUT_PATH"), true);

            int eventsCount = Convert.ToInt32(Console.ReadLine().Trim());

            List<string> events = new List<string>();

            for (int i = 0; i < eventsCount; i++)
            {
                string eventsItem = Console.ReadLine();
                events.Add(eventsItem);
            }

            int drugInfosCount = Convert.ToInt32(Console.ReadLine().Trim());

            List<string> drugInfos = new List<string>();

            for (int i = 0; i < drugInfosCount; i++)
            {
                string drugInfosItem = Console.ReadLine();
                drugInfos.Add(drugInfosItem);
            }

            string result = Result.solution(events, drugInfos);

            textWriter.WriteLine(result);

            textWriter.Flush();
            textWriter.Close();
        }
    }

}
