namespace BulkApi.Core.Processing;

public interface IDataMassager
{
    Dictionary<string, object?> Massage(Dictionary<string, object?> row, string sourceFile);
}
