using Haare.Scripts.Client.Data;

// DataManager.GetModel<MapSaveModel>()이 Save/map.json을 MapDto로 역직렬화한 뒤 이 생성자로 넘겨준다.
[DataModelAttribute(typeof(MapSerializer.MapDto), "", "Save/map.json")]
public class MapSaveModel : IDataModel
{
    public Map Map { get; }

    public MapSaveModel(MapSerializer.MapDto dto)
    {
        Map = MapSerializer.DtoToMap(dto);
    }
}
