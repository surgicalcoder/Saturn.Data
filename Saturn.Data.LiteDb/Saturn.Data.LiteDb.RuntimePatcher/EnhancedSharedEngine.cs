using LiteDB;
using LiteDB.Engine;
using Medallion.Threading;
using Medallion.Threading.FileSystem;

namespace Saturn.Data.LiteDb.RuntimePatcher;

public class EnhancedSharedEngine : ILiteEngine
{
    private readonly EngineSettings _settings;
    private readonly FileDistributedLock mutex;
    private LiteEngine _engine;
    private bool _transactionRunning = false;

    public EnhancedSharedEngine(EngineSettings settings)
    {
        _settings = settings;
        mutex = new FileDistributedLock(new FileInfo($"{settings.Filename}.lock"));
    }

    /// <summary>
    /// Open database in safe mode
    /// </summary>
    /// <returns>true if successfully opened; false if already open</returns>
    private bool OpenDatabase()
    {
        if (!_transactionRunning && _engine == null)
        {
            _engine = new LiteEngine(_settings);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Dequeue stack and dispose database on empty stack
    /// </summary>
    private void CloseDatabase()
    {
        // Don't dispose the engine while a transaction is running.
        if (!_transactionRunning && _engine != null)
        {
            _engine.Dispose();
            _engine = null;
        }
    }

    #region Transaction Operations

    public bool BeginTrans()
    {
        using (var handle = mutex.Acquire())
        {
            OpenDatabase();
            try
            {
                _transactionRunning = _engine.BeginTrans();
                return _transactionRunning;
            }
            catch
            {
                CloseDatabase();
                throw;
            }
        }
    }

    public bool Commit()
    {
        using (var handle = mutex.Acquire())
        {
            if (_engine == null) return false;
            try
            {
                return _engine.Commit();
            }
            finally
            {
                _transactionRunning = false;
                CloseDatabase();
            }
        }
    }

    public bool Rollback()
    {
        using (var handle = mutex.Acquire())
        {
            if (_engine == null) return false;
            try
            {
                return _engine.Rollback();
            }
            finally
            {
                _transactionRunning = false;
                CloseDatabase();
            }
        }
    }

    #endregion

    #region Read Operation

    public IBsonDataReader Query(string collection, Query query)
    {
        var handle = mutex.Acquire();
        bool opened = OpenDatabase();
        var reader = _engine.Query(collection, query);
        return new SharedDataReader(reader, () =>
        {
            handle.Dispose();
            if (opened)
            {
                CloseDatabase();
            }
        });
    }

    public BsonValue Pragma(string name)
    {
        return QueryDatabase(() => _engine.Pragma(name));
    }

    public bool Pragma(string name, BsonValue value)
    {
        return QueryDatabase(() => _engine.Pragma(name, value));
    }

    #endregion

    #region Write Operations

    public int Checkpoint()
    {
        return QueryDatabase(() => _engine.Checkpoint());
    }

    public long Rebuild(RebuildOptions options)
    {
        return QueryDatabase(() => _engine.Rebuild(options));
    }

    public int Insert(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId)
    {
        return QueryDatabase(() => _engine.Insert(collection, docs, autoId));
    }

    public int Update(string collection, IEnumerable<BsonDocument> docs)
    {
        return QueryDatabase(() => _engine.Update(collection, docs));
    }

    public int UpdateMany(string collection, BsonExpression extend, BsonExpression predicate)
    {
        return QueryDatabase(() => _engine.UpdateMany(collection, extend, predicate));
    }

    public int Upsert(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId)
    {
        return QueryDatabase(() => _engine.Upsert(collection, docs, autoId));
    }

    public int Delete(string collection, IEnumerable<BsonValue> ids)
    {
        return QueryDatabase(() => _engine.Delete(collection, ids));
    }

    public int DeleteMany(string collection, BsonExpression predicate)
    {
        return QueryDatabase(() => _engine.DeleteMany(collection, predicate));
    }

    public bool DropCollection(string name)
    {
        return QueryDatabase(() => _engine.DropCollection(name));
    }

    public bool RenameCollection(string name, string newName)
    {
        return QueryDatabase(() => _engine.RenameCollection(name, newName));
    }

    public bool DropIndex(string collection, string name)
    {
        return QueryDatabase(() => _engine.DropIndex(collection, name));
    }

    public bool EnsureIndex(string collection, string name, BsonExpression expression, bool unique)
    {
        return QueryDatabase(() => _engine.EnsureIndex(collection, name, expression, unique));
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~EnhancedSharedEngine()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_engine != null)
            {
                _engine.Dispose();
                _engine = null;
            }
        }
    }

    private T QueryDatabase<T>(Func<T> Query)
    {
        using (var handle = mutex.Acquire())
        {
            bool opened = OpenDatabase();
            try
            {
                return Query();
            }
            finally
            {
                if (opened)
                {
                    CloseDatabase();
                }
            }
        }
    }
}