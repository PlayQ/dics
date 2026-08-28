using System;

namespace DICS
{
    /// <summary>
    /// Shared dispatch logic for synchronous <see cref="Instruction"/>s, used by both
    /// <see cref="Producer"/> and <see cref="AsyncProducer"/>. Async-only instructions
    /// (<see cref="Instruction.CallAsyncFunctoid"/>, <see cref="Instruction.CallAsyncInitializer"/>)
    /// are NOT handled here and must be dispatched by the caller.
    /// </summary>
    internal static class InstructionDispatcher
    {
        /// <returns><c>true</c> if the instruction was a synchronous one and was executed;
        /// <c>false</c> if it is an async-only instruction that the caller must handle.</returns>
        public static bool TryExecuteSync(
            LocatorImpl locatorImpl,
            Instruction defn,
            Key key,
            DepMatrix<Instruction> dependees,
            ILocator parent)
        {
            switch (defn)
            {
                case Instruction.CallFunctoid callFunctoid:
                    locatorImpl.Put(callFunctoid.Key, callFunctoid.Functoid.Invoke(locatorImpl));
                    return true;
                case Instruction.CreateSet createSet:
                    var newSet = createSet.SetZygote.Create();
                    foreach (var setElement in createSet.Elements)
                        newSet.Add(locatorImpl.Resolve<object>(setElement));
                    locatorImpl.Put(createSet.Key, newSet.Retrieve());
                    return true;
                case Instruction.UseInstance useInstance:
                    locatorImpl.Put(useInstance.Key, useInstance.Instance);
                    return true;
                case Instruction.Import importValue:
                    if (importValue.Key == Key.Of<MagicMutableDicsReference<LocatorMeta>>())
                    {
                        locatorImpl.Put(importValue.Key, new MagicMutableDicsReference<LocatorMeta>());
                        return true;
                    }
                    if (importValue.Key == Key.Of<MagicMutableDicsReference<ILocator>>())
                    {
                        locatorImpl.Put(importValue.Key, new MagicMutableDicsReference<ILocator>());
                        return true;
                    }
                    if (importValue.Key == Key.Of<ILocator>("parent"))
                    {
                        locatorImpl.Put(importValue.Key, parent);
                        return true;
                    }

                    var imported = parent.TryResolve<object>(importValue.Key, out var importedValue);
                    if (imported)
                        locatorImpl.Put(importValue.Key, importedValue!);
                    else
                        throw new DicsProducerException(
                            $"Failed to import key {key} from parent locators hierarchy. Required by:\n{dependees.Links[key].NiceList()}");
                    return true;

                case Instruction.UseKey useKey:
                    if (locatorImpl.TryResolve<object>(useKey.Source, out var resolved))
                    {
                        locatorImpl.Put(useKey.Key, resolved!);
                        return true;
                    }
                    throw new DicsProducerException($"Cannot resolve key {useKey.Source}");

                case Instruction.CreateFunctoidFactory createFunctoidFactory:
                    locatorImpl.Put(createFunctoidFactory.Key,
                        createFunctoidFactory.Zygote.Create(locatorImpl));
                    return true;

                case Instruction.CreateLifecycleFactory createLifecycleFactory:
                    locatorImpl.Put(createLifecycleFactory.Key,
                        createLifecycleFactory.Zygote.Create(locatorImpl));
                    return true;

                case Instruction.CallInitializer callInitializer:
                    var instance = locatorImpl.Get<object>(callInitializer.ExtractorKey);
                    callInitializer.Initializer.Initialize(instance, locatorImpl);
                    locatorImpl.Put(callInitializer.Key, instance);
                    return true;

                case Instruction.ToDo toDo:
                    throw new DicsProducerException($"{key} is not implemented yet: {toDo.Message}");

                case Instruction.FactoryToGeneratedFunctoid factoryToGeneratedFunctoid:
                    locatorImpl.Put(factoryToGeneratedFunctoid.Key,
                        factoryToGeneratedFunctoid.Functoid.Make(locatorImpl, null));
                    return true;

                case Instruction.FactoryToGeneratedLifecycle factoryToGeneratedLifecycle:
                    locatorImpl.Put(factoryToGeneratedLifecycle.Key,
                        factoryToGeneratedLifecycle.Functoid.Make(locatorImpl, factoryToGeneratedLifecycle.Extractor));
                    return true;

                case Instruction.CallAsyncFunctoid:
                case Instruction.CallAsyncInitializer:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(defn));
            }
        }
    }
}
