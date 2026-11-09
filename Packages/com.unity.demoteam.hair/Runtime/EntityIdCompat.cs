using UnityEngine;

// Local patch (Sleeping Calamity): Unity 6000.6 made Object.GetInstanceID() a compile error.
// The package only uses the id as an int key or hash input, so keep that with the low 32 bits of the EntityId.
public static class EntityIdCompat
{
	public static int GetInstanceKey(this Object obj) => (int)obj.GetEntityId().GetRawData();
}
