namespace OpenDeFrance.Tests;

/// <summary>
/// Définition de la collection xUnit qui force l'exécution séquentielle
/// des tests accédant à SQLite (évite la contention sur Task.Run asynchrones).
/// </summary>
[CollectionDefinition("DatabaseTests", DisableParallelization = true)]
public class DatabaseTestCollection { }
