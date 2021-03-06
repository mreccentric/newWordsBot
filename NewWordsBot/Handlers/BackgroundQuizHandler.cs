﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Telegram.Bot.Types;

namespace NewWordsBot
{
    public class BackgroundQuizHandler : IHandler
    {
        private readonly IUsersStorage usersStorage;
        private readonly IWordsStorage wordsStorage;
        private readonly IWordsDictionary dictionary;
        private readonly IPendingQuizRequests pendingQuizRequests;
        private readonly IRandomWordDefinitionSelector _randomWordDefinitionDefenitionSelector;
        private readonly ILearningMethodology learningMethodology;
        private readonly Random rnd = new Random();
        private readonly ILog logger = LogManager.GetLogger(typeof(BackgroundQuizHandler));
        private readonly BackgroundQuizHandlerMessanger messanger;

        public BackgroundQuizHandler(IUsersStorage usersStorage, IWordsStorage wordsStorage, IWordsDictionary dictionary,
            IPendingQuizRequests pendingQuizRequests, IRandomWordDefinitionSelector randomWordDefinitionDefenitionSelector, ILearningMethodology learningMethodology, BackgroundQuizHandlerMessanger messanger)
        {
            this.usersStorage = usersStorage;
            this.wordsStorage = wordsStorage;
            this.dictionary = dictionary;
            this.pendingQuizRequests = pendingQuizRequests;
            this._randomWordDefinitionDefenitionSelector = randomWordDefinitionDefenitionSelector;
            this.learningMethodology = learningMethodology;
            this.messanger = messanger;
        }

        public void Start()
        {
            Task.Run(() => Routine());
        }

        public bool TryHandleCallback(CallbackQuery callback)
        {
            if (!IsOurCallback(callback))
                return false;
            User user = null;
            try
            {
                user = usersStorage.GetOrRegisterUser(callback.Message.Chat);
                int variantIndex;
                string responseForWord;
                logger.Debug(callback.Data);
                ParseCallback(callback, out variantIndex, out responseForWord);
                Word word;
                int rightVariantIndex;
                if (pendingQuizRequests.TryGet(user, out word, out rightVariantIndex))
                {
                    if (word.TheWord == responseForWord)
                    {
                        Word transformatedWordToStore;
                        if (variantIndex == rightVariantIndex)
                        {
                            messanger.SendRightResponse(user);
                            transformatedWordToStore = learningMethodology.OnRightResponse(word);
                        }
                        else
                        {
                            messanger.SendWrongReponse(user);
                            transformatedWordToStore = learningMethodology.OnWrongResponse(word);
                        }
                        wordsStorage.AddOrUpdate(user, transformatedWordToStore);
                        pendingQuizRequests.Remove(user);
                    }
                    else
                    {
                        //just ignore
                    }
                        
                }
                else
                {
                    //just ignore
                }
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e);
                if (user != null)
                    messanger.SendError(user);
            }
            return false;
        }

        private static void ParseCallback(CallbackQuery callback, out int variantIndex, out string responseForWord)
        {
            var response = callback.Data.Replace("/response ", "");
            var strings = response.Split(" ", 2);
            variantIndex = int.Parse(strings[0]);
            responseForWord = strings[1];
        }

        private bool IsOurCallback(CallbackQuery callback)
        {
            try
            {
                if (callback.Data.StartsWith("/response"))
                    return true;
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
            return false;
        }

        private void Routine()
        {
            while (true)
            {
                try
                {
                    RiddleRound();
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
                Thread.Sleep(1000); //TODO: get rid of
            }
        }

        private void RiddleRound()
        {
            var users = usersStorage.GetAllUsers();
            foreach (var user in users)
            {
                if (!pendingQuizRequests.ContainsRequest(user))
                {
                    Word word;
                    if ((word = wordsStorage.GetNextReadyToRepeat(user)) != null)
                    {
                        List<string> definitions = GetWrongDefinitions(word);
                        AddWordDefinitionsAtRandomPosition(definitions, word);
                        var rightVariantIndex = definitions.IndexOf(word.Definition);
                        messanger.AskUser(user, word.TheWord, definitions, rightVariantIndex);
                        pendingQuizRequests.Add(user, word, rightVariantIndex);
                    }
                }
            }
        }

        private void AddWordDefinitionsAtRandomPosition(List<string> definitions, Word word)
        {
            int rndIndex = rnd.Next(0, definitions.Count);
            string tmp = definitions[rndIndex];
            definitions[rndIndex] = word.Definition;
            definitions.Add(tmp);
        }

        private List<string> GetWrongDefinitions(Word word)
        {
            var defs = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var w = _randomWordDefinitionDefenitionSelector.Select(word.Form);
                defs.Add(w); 
            }
            return defs;
        }

        public bool TryHandleRequest(Message message)
        {
            return false;
        }
    }
}