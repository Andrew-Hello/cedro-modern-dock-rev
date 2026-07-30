namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>Direct port of DockServiceTest.java</summary>
public class DockServiceTest
{
    [Fact]
    public void SnapsSavedDockCoordinatesToWholePixels()
    {
        var repository = new InMemoryDockRepository();
        var service = new DockService(repository);

        service.SetDockPosition(718.5, 28.2);

        DockModel savedDock = repository.SavedModel!;
        Assert.Equal(719.0, savedDock.DockPositionX);
        Assert.Equal(28.0, savedDock.DockPositionY);
    }

    private sealed class InMemoryDockRepository : IDockRepository
    {
        private readonly DockModel _model = new();
        public DockModel? SavedModel { get; private set; }

        public DockModel Load()
        {
            SavedModel ??= _model;
            return _model;
        }

        public void Save(DockModel model) => SavedModel = model;
    }
}
