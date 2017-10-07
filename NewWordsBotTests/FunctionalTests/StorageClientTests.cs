﻿using System;
using FluentAssertions;
using MongoDB.Driver;
using NewWordsBot;
using Xunit;

namespace NewWordsBotTests.FunctionalTests
{
    public class StorageClientTests
    {
        [Fact]
        public void GetUsers_should_return_all_users_after_InsertUser_call()
        {
            var u1 = new User(Guid.NewGuid(), "user1", 123, DateTime.UtcNow);
            var u2 = new User(Guid.NewGuid(), "user2", 123, DateTime.UtcNow);
            var u3 = new User(Guid.NewGuid(), "user3", 123, DateTime.UtcNow);
            
            var mongoClient = new MongoClient(Config.MongoDbConnectionString);
            
            if (!Config.DatabaseName.EndsWith("-test"))
                throw new Exception("A-a-a-a, don't clear working database");
            mongoClient
                .GetDatabase(Config.DatabaseName)
                .GetCollection<User>(Config.UsersCollection)
                .DeleteMany(u => true);
            
            var storageClient = new StorageClient(mongoClient, Config.DatabaseName, Config.UsersCollection, Config.WordsForUserCollectionPrefix);
            
            storageClient.InsertUser(u1);
            var users = storageClient.GetUsers();
            users.Should().Equal(u1);
            
            storageClient.InsertUser(u2);
            storageClient.GetUsers().Should().Equal(u1, u2);
            
            storageClient.InsertUser(u3);
            storageClient.GetUsers().Should().Equal(u1, u2, u3);
        }

        [Fact]
        public void FindWordWithNextRepetitionLessThenNow_should_return_null_if_no_words()
        {
            var user = new User(Guid.NewGuid(), "testUser", 123, DateTime.UtcNow);
            
            ClearWordsCollection(GetCollectionName(user));
            
            var storageClient = CreateStorageClient();

            storageClient.FindWordWithNextRepetitionLessThenNow(user).Should().BeNull();
        }

        [Fact]
        public void FindWordWithNextRepetitionLessThenNow_should_return_null_if_for_stale_words()
        {
            var user = new User(Guid.NewGuid(), "testUser", 123, DateTime.UtcNow);

            ClearWordsCollection(GetCollectionName(user));

            var storageClient = CreateStorageClient();
            
            storageClient.AddOrUpdateWord(user, CeateRandomWord(DateTime.UtcNow.AddMinutes(1)));
            
            storageClient.FindWordWithNextRepetitionLessThenNow(user).Should().BeNull();
        }
        
        [Fact]
        public void FindWordWithNextRepetitionLessThenNow_should_return_word_if_fresh()
        {
            var user = new User(Guid.NewGuid(), "testUser", 123, DateTime.UtcNow);

            ClearWordsCollection(GetCollectionName(user));

            var storageClient = CreateStorageClient();

            var word = CeateRandomWord(DateTime.UtcNow.AddMinutes(-11));
            storageClient.AddOrUpdateWord(user, word);

            storageClient.FindWordWithNextRepetitionLessThenNow(user).Should().Be(word);
        }

        private Word CeateRandomWord(DateTime nextRepetition)
        {
            return new Word(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), PartOfSpeech.Noun, LearningStage.First_1m, nextRepetition, DateTime.UtcNow);
        }

        private static StorageClient CreateStorageClient()
        {
            var mongoClient = new MongoClient(Config.MongoDbConnectionString);
            var storageClient = new StorageClient(mongoClient, Config.DatabaseName, Config.UsersCollection,
                Config.WordsForUserCollectionPrefix);
            return storageClient;
        }

        private static string GetCollectionName(User user)
        {
            var collectionName = Config.WordsForUserCollectionPrefix + user.Username;
            return collectionName;
        }

        private static void ClearWordsCollection(string collectionName)
        {
            var mongoClient = new MongoClient(Config.MongoDbConnectionString);
            
            if (!Config.DatabaseName.EndsWith("-test"))
                throw new Exception("A-a-a-a, don't clear working database");
            mongoClient
                .GetDatabase(Config.DatabaseName)
                .GetCollection<User>(collectionName)
                .DeleteMany(u => true);
        }
    }
}