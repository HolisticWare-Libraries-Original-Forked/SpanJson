﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace SpanJson.Formatters
{
    public sealed partial class ListFormatter<TList, T, TSymbol, TResolver> : IAsyncJsonFormatter<TList, TSymbol> where TResolver : IJsonFormatterResolver<TSymbol, TResolver>, new() where TSymbol : struct where TList : class, IList<T>
    {
        public async Task SerializeAsync(AsyncJsonWriter<TSymbol> asyncWriter, TList value, CancellationToken cancellationToken = default)
        {
            // let it run sync until the serialized size hits ~32kb
            // if a single entity is
            // flushasync
            // new jsonwriter, start from pos 0
            if (value == null)
            {
                await WriteNull(asyncWriter, cancellationToken);
                return;
            }
            Queue<(Task, int)> queue = new Queue<(Task, int)>();
            queue.Enqueue((Task.CompletedTask, 0));
            var valueLength = value.Count;
            while (queue.Count > 0)
            {
                var (lastTask, index) = queue.Dequeue();
                if (!lastTask.IsCompletedSuccessfully)
                {
                    await lastTask.ConfigureAwait(false);
                }

                if (index < valueLength)
                {
                    var next = WriteElements(asyncWriter, value, index, cancellationToken);
                    queue.Enqueue(next);
                }
            }

            Task WriteNull(AsyncJsonWriter<TSymbol> aasyncWriter, CancellationToken ccancellationToken = default)
            {
                var writer = aasyncWriter.Create();
                writer.WriteNull();
                return aasyncWriter.FlushAsync(writer.Position, ccancellationToken);
            }
        }

        private static Task WriteElement(AsyncJsonWriter<TSymbol> asyncWriter,  ref JsonWriter<TSymbol> writer, T value, CancellationToken cancellationToken = default)
        {
            SerializeRuntimeDecisionInternal<T, TSymbol, TResolver>(ref writer, value, ElementFormatter);
            if (writer.Position >= asyncWriter.MaxSafeWriteSize)
            {
                return asyncWriter.FlushAsync(writer.Position, cancellationToken);
            }

            return Task.CompletedTask;
        }

        private static (Task, int) WriteElements(AsyncJsonWriter<TSymbol> asyncWriter, TList value, int index, CancellationToken cancellationToken = default)
        {
            var writer = asyncWriter.Create();
            var valueLength = value.Count;
            if (index == 0)
            {
                writer.IncrementDepth(); // this is not correct, needs to be done in asyncwriter
                writer.WriteBeginArray();
            }

            for (var i = index; i < valueLength; i++)
            {
                writer.WriteValueSeparator();
                var task = WriteElement(asyncWriter, ref writer, value[i], cancellationToken);
                if (!task.IsCompletedSuccessfully)
                {
                    return (task, i + 1);
                }
            }

            writer.DecrementDepth();
            writer.WriteEndArray();
            return (asyncWriter.FlushAsync(writer.Position, cancellationToken), valueLength);
        }

        public Task<TList> DeserializeAsync(AsyncJsonReader<TSymbol> asyncReader, CancellationToken cancellationToken = default)
        {
            // only problem is string, we need to find the end of the string and buffer until we hit it
            return default;
        }
    }
}


