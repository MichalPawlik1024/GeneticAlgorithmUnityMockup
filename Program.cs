class Program
{
    const double OptimalTurn       = 0.45;
    const double OptimalAccelerate = 0.30;
    const double OptimalDecelerate = 0.70;
    const int    Scenarios         = 300;

    static double Evaluate(DecisionSet ds, Random noiseRng)
    {
        var rng = new Random(1337);
        double score = 0;

        for (int i = 0; i < Scenarios; i++)
        {
            double tStim = rng.NextDouble();
            double aStim = rng.NextDouble();
            double dStim = rng.NextDouble();

            if ((tStim > ds.turnThreshold)       == (tStim > OptimalTurn))       score += 1;
            if ((aStim > ds.accelerateThreshold) == (aStim > OptimalAccelerate)) score += 1;
            if ((dStim > ds.decelerateThreshold) == (dStim > OptimalDecelerate)) score += 1;
        }

        score += noiseRng.NextDouble() * 0.5;
        return score;
    }

    static void Main(string[] args)
    {
        var noiseRng  = new Random();
        var threshMin = new DecisionSet(0, 0.0, 0.0, 0.0);
        var threshMax = new DecisionSet(0, 1.0, 1.0, 1.0);

        var ga = new GeneticAlgorithm(
            populationSize:                        30,
            thresholdGenerationMin:                threshMin,
            thresholdGenerationMax:                threshMax,
            numberOfDrawedDecisionSetsInSelection: 6,
            hybrydazationChancePercent:            75,
            mutationChancePercent:                 12
        );

        const int generations = 25;
        int maxScore = Scenarios * 3;

        Console.WriteLine($"Cel: turn={OptimalTurn:F2}  accel={OptimalAccelerate:F2}  decel={OptimalDecelerate:F2}");
        Console.WriteLine($"Max możliwy score: {maxScore}");
        Console.WriteLine();
        Console.WriteLine($"{"Gen",3} | {"Best",6} | {"Avg",6} | {"turn",6} | {"accel",6} | {"decel",6}");
        Console.WriteLine(new string('-', 52));

        for (int gen = 0; gen <= generations; gen++)
        {
            foreach (var ds in ga.getDecisionSets())
                ds.score = Evaluate(ds, noiseRng);

            var best = ga.getDecisionSets().MaxBy(d => d.score)!;
            double avg = ga.getDecisionSets().Average(d => d.score);

            Console.WriteLine($"{gen,3} | {best.score,6:F1} | {avg,6:F1} | {best.turnThreshold,6:F3} | {best.accelerateThreshold,6:F3} | {best.decelerateThreshold,6:F3}");

            if (gen < generations)
                ga.run();
        }

        Console.WriteLine(new string('-', 52));
        Console.WriteLine($"{"Cel",3} | {"",6} | {"",6} | {OptimalTurn,6:F3} | {OptimalAccelerate,6:F3} | {OptimalDecelerate,6:F3}");
    }
}
