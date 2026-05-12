using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[AttributeUsage(AttributeTargets.Field)]
public class GeneAttribute : Attribute { }

[System.Serializable]
public class DecisionSet
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

public static class GeneticUtils
{
    private static readonly System.Random _rng = new System.Random();

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
            throw new System.ArgumentException($"Żądano {n} elementów, ale dostępnych jest tylko {available.Count} po wykluczeniu.");

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

public class GeneticAlgorithm
{
    public List<DecisionSet> decisionSets;
    public List<DecisionSet> decisionSetsNextGeneration;
    public int populationSize;
    public DecisionSet bestDecisionSet;
    public int numberOfDrawedDecisionSetsInSelection;
    public int hybrydazationChancePercent;
    public int mutationChancePercent;

    private readonly FieldInfo[] _geneFields;
    private readonly System.Type _instanceType;

    private DecisionSet CreateEmpty()
        => (DecisionSet)System.Activator.CreateInstance(_instanceType);

    private DecisionSet Clone(DecisionSet source)
    {
        var clone = CreateEmpty();
        clone.score = source.score;
        foreach (var field in _geneFields)
            field.SetValue(clone, field.GetValue(source));
        return clone;
    }

    private DecisionSet generateNewDecisionSet(DecisionSet thresholdGenerationMin, DecisionSet thresholdGenerationMax)
    {
        double initialScore = thresholdGenerationMin.score == thresholdGenerationMax.score
            ? thresholdGenerationMin.score
            : 0.0;

        var ds = CreateEmpty();
        ds.score = initialScore;
        foreach (var field in _geneFields)
        {
            double min = (double)field.GetValue(thresholdGenerationMin);
            double max = (double)field.GetValue(thresholdGenerationMax);
            field.SetValue(ds, GeneticUtils.GetRandom(min, max));
        }
        return ds;
    }

    private void generateNewGeneration(DecisionSet thresholdGenerationMin, DecisionSet thresholdGenerationMax)
    {
        for (int i = 0; i < populationSize; i++)
            decisionSets.Add(generateNewDecisionSet(thresholdGenerationMin, thresholdGenerationMax));
    }

    private (DecisionSet, DecisionSet) selection()
    {
        var candidates1 = GeneticUtils.DrawRandom(decisionSets, numberOfDrawedDecisionSetsInSelection, new List<int>());
        var parent1 = candidates1.OrderByDescending(d => d.score).First();

        var candidates2 = GeneticUtils.DrawRandom(decisionSets, numberOfDrawedDecisionSetsInSelection, new List<int>());
        var parent2 = candidates2.OrderByDescending(d => d.score).First();

        return (parent1, parent2);
    }

    public void evolve()
    {
        decisionSetsNextGeneration = new List<DecisionSet>();
        while (decisionSetsNextGeneration.Count < populationSize - 1)
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
                    double g1 = (double)field.GetValue(parent1);
                    double g2 = (double)field.GetValue(parent2);
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
                    double current = (double)field.GetValue(ds);
                    field.SetValue(ds, current + GeneticUtils.GetRandom(-1.0, 1.0));
                }
            }
        }
    }

    public void elitarism()
    {
        bestDecisionSet = decisionSets.OrderByDescending(d => d.score).First();
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
        return decisionSets;
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
