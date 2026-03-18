using System;
using System.Collections.Generic;

public interface IDtoConvertible<TDto>
{
    TDto ToDto();
    void ApplyDto(TDto dto);
}

public static class DataMapperUtil
{
    public static Dictionary<TKey, TDtoValue> ToDtoDictionary<TKey, TRuntimeValue, TDtoValue>(
        Dictionary<TKey, TRuntimeValue> source)
        where TRuntimeValue : IDtoConvertible<TDtoValue>
    {
        var result = new Dictionary<TKey, TDtoValue>();

        if (source == null)
            return result;

        foreach (var pair in source)
        {
            if (pair.Value == null)
                continue;

            result[pair.Key] = pair.Value.ToDto();
        }

        return result;
    }

    public static void ApplyDtoDictionary<TKey, TRuntimeValue, TDtoValue>(
        Dictionary<TKey, TRuntimeValue> target,
        Dictionary<TKey, TDtoValue> source,
        Func<TDtoValue, TRuntimeValue> createRuntimeValue)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (createRuntimeValue == null)
            throw new ArgumentNullException(nameof(createRuntimeValue));

        target.Clear();

        if (source == null)
            return;

        foreach (var pair in source)
        {
            if (pair.Value == null)
                continue;

            target[pair.Key] = createRuntimeValue(pair.Value);
        }
    }
}