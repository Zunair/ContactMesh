using ContactMesh.Core.Models;

namespace ContactMesh.Core.Abstractions;

public interface IContactNormalizer
{
    MeshContact Normalize(MeshContact contact);
}
