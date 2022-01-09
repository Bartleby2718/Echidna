﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Data.Mapping;

internal static class MappingDelegateProvider
{
    // TODO incorporate settings
    private static readonly MicroCache<(Type ReaderType, RowSchema Schema, Type DestinationType), object> Cache = new(maxCount: 10000);

    public static Func<TDestination> GetMappingDelegate<TDestination>(DbDataReader reader)
    {
        var readerType = reader.GetType(); // use this over typeof(TReader) so that we can avoid virtual calls in the delegate
        var schema = RowSchema.FromReader(reader);
        var destinationType = typeof(TDestination);

        MappingDelegateFactory<TDestination> factory;

        if (Cache.TryGetValue((readerType, schema, destinationType), out var cached))
        {
            factory = (MappingDelegateFactory<TDestination>)cached;
        }
        else
        {
            var createdDelegate = MappingDelegateCreator.CreateMappingDelegate(readerType, schema, destinationType, isExactReaderType: true);

            factory = (MappingDelegateFactory<TDestination>)Activator.CreateInstance(typeof(MappingDelegateFactory<,>).MakeGenericType(readerType, destinationType), createdDelegate)!;

            if (!Cache.TryAdd((readerType, schema, destinationType), factory, out cached))
            {
                factory = (MappingDelegateFactory<TDestination>)cached;
            }
        }

        return factory.CreateDelegate(reader);
    }

    private abstract class MappingDelegateFactory<TDestination>
    {
        public abstract Func<TDestination> CreateDelegate(DbDataReader reader);
    }

    private sealed class MappingDelegateFactory<TReader, TDestination> : MappingDelegateFactory<TDestination>
        where TReader : DbDataReader
    {
        private readonly Func<TReader, TDestination> _delegate;

        public MappingDelegateFactory(Func<TReader, TDestination> @delegate)
        {
            this._delegate = @delegate;
        }

        public override Func<TDestination> CreateDelegate(DbDataReader reader)
        {
            var typedReader = (TReader)reader;
            var @delegate = this._delegate; // store in local to avoid extra indirection through this
            return () => @delegate(typedReader);
        }
    }
}