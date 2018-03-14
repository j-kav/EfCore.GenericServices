﻿// Copyright (c) 2018 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT licence. See License.txt in the project root for license information.

using System;
using System.Linq;
using AutoMapper;
using GenericLibsBase;
using GenericServices.Internal;
using GenericServices.Internal.Decoders;
using GenericServices.Internal.MappingCode;
using Microsoft.EntityFrameworkCore;

namespace GenericServices.PublicButHidden
{
    public class GenericService<TContext> : StatusGenericHandler where TContext : DbContext
    {
        private readonly TContext _context;
        private readonly IMapper _mapper;

        public GenericService(TContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public T GetSingle<T>(params object[] keys) where T : class
        {
            T result = null;
            var entityInfo = typeof(T).GetUnderlyingEntityInfo(_context);
            if (entityInfo.EntityType == typeof(T))
            {
                result = _context.Set<T>().Find(keys);
            }
            else
            {
                //else its a DTO, so we need to project the entity to the DTO and select the single element
                var projector = new CreateProjector(_context, _mapper, typeof(T), entityInfo);
                result = ((IQueryable<T>) projector.Accessor.GetViaKeysWithProject(keys)).SingleOrDefault();
            }

            if (result == null)
            {
                AddError($"Sorry, I could not find the {ExtractDisplayHelpers.GetNameForClass<T>()} you were looking for.");
            }
            return result;
        }

        public IQueryable<T> GetManyNoTracked<T>() where T : class
        {
            var entityInfo = typeof(T).GetUnderlyingEntityInfo(_context);
            if (entityInfo.EntityType == typeof(T))
            {
                return _context.Set<T>().AsNoTracking();
            }

            //else its a DTO, so we need to project the entity to the DTO 
            var projector = new CreateProjector(_context, _mapper, typeof(T), entityInfo);
            return (IQueryable<T>)projector.Accessor.GetManyProjectedNoTracking();
        }

        public void Create<T>(T entityOrDto) where T : class
        {
            var entityInfo = typeof(T).GetUnderlyingEntityInfo(_context);
            if (entityInfo.EntityType == typeof(T))
            {
                _context.Add(entityOrDto);
                _context.SaveChanges();
            }
            else
            {
                var parameterlessCtor =
                    entityInfo.PublicCtors.SingleOrDefault(x => !x.GetParameters().Any());
                if (parameterlessCtor == null)
                    throw new NotImplementedException();
                var entityInstance = Activator.CreateInstance(entityInfo.EntityType);
                var copier = new CreateCopier(_context, _mapper, typeof(T), entityInfo);
                copier.Accessor.MapDtoToEntity(entityOrDto, entityInstance);
                _context.Add(entityInstance);
                _context.SaveChanges();
                //Needs to copy back keys afterwards
            }
        }

        public T Update<T>(T entityOrDto) where T : class
        {
            var entityInfo = typeof(T).GetUnderlyingEntityInfo(_context);
            if (entityInfo.EntityType == typeof(T))
            {
                if (_context.Entry(entityOrDto).State == EntityState.Detached)
                    _context.Update(entityOrDto);
                _context.SaveChanges();
                return entityOrDto;
            }
            else
            { 
                var copier = new CreateCopier(_context, _mapper, typeof(T), entityInfo);
                var entity = copier.Accessor.LoadExistingAndMap(entityOrDto);
                _context.SaveChanges();
                throw new NotImplementedException("Needs to get keys");
            }
        }

        public void Delete<T>(params object[] keys) where T : class
        {
            var entityInfo = typeof(T).GetUnderlyingEntityInfo(_context);
            if (entityInfo.EntityType == typeof(T))
            {
                var entity = _context.Set<T>().Find(keys);
                if (entity == null)
                {
                    AddError($"Sorry, I could not find the {ExtractDisplayHelpers.GetNameForClass<T>()} you wanted to delete.");
                    return;
                }
                _context.Remove(entity);
                _context.SaveChanges();
                return;
            }

            throw new NotImplementedException();
        }

    }
}