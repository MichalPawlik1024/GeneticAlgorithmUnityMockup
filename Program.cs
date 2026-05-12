using System.Reflection;

[AttributeUsage(AttributeTargets.Field)]
class GeneAttribute : Attribute { }

[System.Serializable]
class DecisionSet
{
    public double score;
    [Gene] public double turnThreshold;
    [Gene] public double accelerateThreshold;
    [Gene] public double decelerateThreshold;

    public DecisionSet() { }

    public DecisionSet(double score, double turnThreshold, double accelerateThreshold, double decelerateThreshold)
    {
        this.score = score;
        this.turnThreshold = turnThreshold;
        this.accelerateThreshold = accelerateThreshold;
        this.decelerateThreshold = decelerateThreshold;
    }
}

static class GeneticUtils
{
    private static readonly Random _rng = new Random();

    public static int GetRandom(int min, int maxExclusive)
        => _rng.Next(min, maxExclusive);

    public static double GetRandom(double min, double max)
        => min + _rng.NextDouble() * (max - min);

    public static List<T> DrawRandom<T>(List<T> source, int n, List<int> excludeIndices)
    {
        var available = Enumerable.Range(0, source.Count)
            .Where(i => !excludeIndices.Contains(i))
            .ToList();

        if (n > available.Count)
            throw new ArgumentException($"Żądano {n} elementów, ale dostępnych jest tylko {available.Count} po wykluczeniu.");

        var result = new List<T>(n);
        while (result.Count < n)
        {
            int pick = _rng.Next(available.Count);
            result.Add(source[available[pick]]);
            available.RemoveAt(pick);
        }
        return result;
    }
}

class GeneticAlgorithm
{
    public List<DecisionSet> decisionSets;
    public List<DecisionSet> decisionSetsNextGeneration;
    public int populationSize;
    public DecisionSet? bestDecisionSet;
    public int numberOfDrawedDecisionSetsInSelection;
    public int hybrydazationChancePercent;
    public int mutationChancePercent;

    private readonly FieldInfo[] _geneFields;
    private readonly Type _instanceType;

    private DecisionSet CreateEmpty()
        => (DecisionSet)Activator.CreateInstance(_instanceType)!;

    private DecisionSet Clone(DecisionSet source)
    {
        var clone = CreateEmpty();
        clone.score = source.score;
        foreach (var field in _geneFields)
            field.SetValue(clone, field.GetValue(source));
        return clone;
    }

    DecisionSet generateNewDecisionSet(DecisionSet thresholdGenerationMin, DecisionSet thresholdGenerationMax)
    {
        double initialScore = thresholdGenerationMin.score == thresholdGenerationMax.score
            ? thresholdGenerationMin.score
            : 0.0;

        var ds = CreateEmpty();
        ds.score = initialScore;
        foreach (var field in _geneFields)
        {
            double min = (double)field.GetValue(thresholdGenerationMin)!;
            double max = (double)field.GetValue(thresholdGenerationMax)!;
            field.SetValue(ds, GeneticUtils.GetRandom(min, max));
        }
        return ds;
    }

    void generateNewGeneration(DecisionSet thresholdGenerationMin, DecisionSet thresholdGenerationMax)
    {
        for (int i = 0; i < populationSize; i++)
            decisionSets.Add(generateNewDecisionSet(thresholdGenerationMin, thresholdGenerationMax));
    }

    (DecisionSet, DecisionSet) selection()
    {
        var candidates1 = GeneticUtils.DrawRandom(decisionSets, numberOfDrawedDecisionSetsInSelection, new List<int>());
        var parent1 = candidates1.MaxBy(d => d.score)!;

        var candidates2 = GeneticUtils.DrawRandom(decisionSets, numberOfDrawedDecisionSetsInSelection, new List<int>());
        var parent2 = candidates2.MaxBy(d => d.score)!;

        return (parent1, parent2);
    }

    public void evolve()
    {
        decisionSetsNextGeneration = new List<DecisionSet>();
        //leave the best to me
        while (decisionSetsNextGeneration.Count < populationSize-1)
        {
            var (parent1, parent2) = selection();

            DecisionSet child1, child2;
            if (GeneticUtils.GetRandom(0, 100) < hybrydazationChancePercent)
            {
                child1 = CreateEmpty();
                child2 = CreateEmpty();
                child1.score = 0.0;
                child2.score = 0.0;

                foreach (var field in _geneFields)
                {
                    double g1 = (double)field.GetValue(parent1)!;
                    double g2 = (double)field.GetValue(parent2)!;
                    double alpha = GeneticUtils.GetRandom(0.0, 1.0);
                    field.SetValue(child1, g1 + alpha * (g2 - g1));
                    field.SetValue(child2, g1 + (1.0 - alpha) * (g2 - g1));
                }
            }
            else
            {
                child1 = Clone(parent1);
                child2 = Clone(parent2);
            }

            decisionSetsNextGeneration.Add(child1);
            if (decisionSetsNextGeneration.Count < populationSize)
                decisionSetsNextGeneration.Add(child2);
        }
    }

    public void mutate()
    {
        foreach (var ds in decisionSetsNextGeneration)
        {
            foreach (var field in _geneFields)
            {
                if (GeneticUtils.GetRandom(0, 100) < mutationChancePercent)
                {
                    double current = (double)field.GetValue(ds)!;
                    field.SetValue(ds, current + GeneticUtils.GetRandom(-1.0, 1.0));
                }
            }
        }
    }

    public void elitarism()
    {
        this.bestDecisionSet = decisionSets.MaxBy(d => d.score);
        decisionSetsNextGeneration.Add(bestDecisionSet);
    }
    public void run()
    {
        evolve();
        mutate();
        elitarism();
        decisionSets = decisionSetsNextGeneration;
    }

    public List<DecisionSet> getDecisionSets()
    {
        return this.decisionSets;
    }

    public GeneticAlgorithm(int populationSize, DecisionSet thresholdGenerationMin, DecisionSet thresholdGenerationMax,
        int numberOfDrawedDecisionSetsInSelection, int hybrydazationChancePercent = 70, int mutationChancePercent = 10)
    {
        this.decisionSets = new List<DecisionSet>();
        this.decisionSetsNextGeneration = new List<DecisionSet>();
        this.populationSize = populationSize;
        this.numberOfDrawedDecisionSetsInSelection = numberOfDrawedDecisionSetsInSelection;
        this.hybrydazationChancePercent = hybrydazationChancePercent;
        this.mutationChancePercent = mutationChancePercent;
        _instanceType = thresholdGenerationMin.GetType();
        _geneFields = _instanceType
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.IsDefined(typeof(GeneAttribute)))
            .ToArray();
        generateNewGeneration(thresholdGenerationMin, thresholdGenerationMax);
    }
}

class Program
{
    // Symulacja: agent steruje pojazdem na torze.
    // W każdym z 300 scenariuszy dostaje trzy losowe bodźce (0–1).
    // Podejmuje decyzje (skręt / gaz / hamulec) porównując bodziec z progiem.
    // Idealne progi są ukryte — GA ma je sam odkryć.
    const double OptimalTurn       = 0.45;
    const double OptimalAccelerate = 0.30;
    const double OptimalDecelerate = 0.70;
    const int    Scenarios         = 300;

    static double Evaluate(DecisionSet ds, Random noiseRng)
    {
        // Stały seed — te same scenariusze dla każdego agenta (fair comparison)
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

        // Szum łamie remisy w wczesnych generacjach
        score += noiseRng.NextDouble() * 0.5;
        return score;
    }

    static void Main(string[] args)
    {
        var noiseRng   = new Random();
        var threshMin  = new DecisionSet(0, 0.0, 0.0, 0.0);
        var threshMax  = new DecisionSet(0, 1.0, 1.0, 1.0);

        var ga = new GeneticAlgorithm(
            populationSize:                    30,
            thresholdGenerationMin:            threshMin,
            thresholdGenerationMax:            threshMax,
            numberOfDrawedDecisionSetsInSelection: 6,
            hybrydazationChancePercent:        75,
            mutationChancePercent:             12
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
