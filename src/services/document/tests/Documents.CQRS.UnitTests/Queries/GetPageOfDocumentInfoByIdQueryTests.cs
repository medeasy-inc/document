﻿using Documents.CQRS.Queries;
using Documents.DTO.v1;
using FluentAssertions;
using MedEasy.CQRS.Core.Queries;
using MedEasy.DAL.Repositories;
using MedEasy.RestObjects;
using System;
using Xunit;

namespace Documents.CQRS.UnitTests.Queries
{
    public class GetPageOfDocumentInfoQueryTests
    {
        [Fact]
        public void IsQuery() => typeof(GetPageOfDocumentInfoQuery).Should()
            .BeAssignableTo<IQuery<Guid, PaginationConfiguration, Page<DocumentInfo>>>();

        [Fact]
        public void Ctor_Builds_Valid_Instance()
        {
            // Arrange
            PaginationConfiguration pagination = new PaginationConfiguration { Page = 1, PageSize = 10 };

            // Act
            GetPageOfDocumentInfoQuery query = new GetPageOfDocumentInfoQuery(pagination);

            // Assert
            query.Id.Should()
                .NotBeEmpty();

            query.Data.Should()
                .Be(pagination).And
                .NotBeSameAs(pagination);
        } 
    }
}