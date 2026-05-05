using EFT.InventoryLogic;
using System;
using System.Reflection;


namespace GearAndLootValue
{
    internal static class EftReflection
    {
        //read a boolean that just says it isEquipped its reading a private field
        internal static bool ReadBoolFlag(object source, string memberName, out bool value)
        {
            value = false;

            if (source == null)
            {
                return false;
            }

            Type type = source.GetType();
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    object raw = property.GetValue(source, null);
                    if (raw is bool boolValue)
                    {
                        value = boolValue;
                        return true;
                    }
                }

                FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object raw = field.GetValue(source);
                    if (raw is bool boolValue)
                    {
                        value = boolValue;
                        return true;
                    }
                }

                type = type.BaseType;
            }

            return false;
        }

        // EFT keeps renaming these fields between patches, so I just brute-force search here.
        internal static object FindMemberValue(object source, params string[] memberNames)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            while (type != null)
            {
                foreach (string memberName in memberNames)
                {
                    PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property != null && property.GetIndexParameters().Length == 0)
                    {
                        object value = property.GetValue(source, null);
                        if (value != null)
                        {
                            return value;
                        }
                    }

                    FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        object value = field.GetValue(source);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        internal static bool FindSlotName(Item item, out string slotName)
        {
            slotName = null;

            if (!FindSlotFromAddress(item, out Slot slot) || slot == null)
            {
                return false;
            }

            slotName = slot.Name;
            return !string.IsNullOrEmpty(slotName);
        }

        internal static bool FindSlotFromAddress(Item item, out Slot slot)
        {
            slot = null;

            if (item == null || item.CurrentAddress == null)
            {
                return false;
            }

            object address = item.CurrentAddress;
            Type type = address.GetType();

            while (type != null)
            {
                PropertyInfo slotProperty = type.GetProperty(
                    "Slot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotProperty != null && typeof(Slot).IsAssignableFrom(slotProperty.PropertyType))
                {
                    slot = slotProperty.GetValue(address, null) as Slot;
                    return slot != null;
                }

                FieldInfo slotField = type.GetField(
                    "Slot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotField != null && typeof(Slot).IsAssignableFrom(slotField.FieldType))
                {
                    slot = slotField.GetValue(address) as Slot;
                    return slot != null;
                }

                type = type.BaseType;
            }

            return false;
        }

        internal static bool ReadSlotBool(object slot, string fieldName, out bool value)
        {
            value = false;

            if (slot == null)
            {
                return false;
            }

            Type t = slot.GetType();

            while (t != null)
            {
                FieldInfo field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(slot);
                    return true;
                }

                t = t.BaseType;
            }

            return false;
        }

    }
}