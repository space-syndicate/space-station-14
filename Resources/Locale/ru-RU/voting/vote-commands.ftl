## CreateVoteCommand

create-vote-command-description = Создает голосование
create-vote-command-help = Использование: createvote <'restart'|'preset'>
create-vote-command-cannot-call-vote-now = Вы не можете назначить голосование прямо сейчас!
create-vote-command-invalid-vote-type = Вы не можете вызвать голосование прямо сейчас!

## CreateCustomCommand

create-custom-command-description = Создает пользовательское голосование
create-custom-command-help = customvote <title> <option1> <option2> [option3...]
create-custom-command-on-finished-tie = Ничья между {$ties}!
create-custom-command-on-finished-win = {$winner} побеждает!

## VoteCommand

vote-command-description = Голосовать на активном голосовании
vote-command-help = голосовать <voteId> <option>
vote-command-cannot-call-vote-now = Вы не можете вызвать голосование прямо сейчас!
vote-command-on-execute-error-must-be-player = Должен быть игроком
vote-command-on-execute-error-invalid-vote-id = Неверный идентификатор голоса
vote-command-on-execute-error-invalid-vote-options = Неверные параметры голосования
vote-command-on-execute-error-invalid-vote = Неверное голосование
vote-command-on-execute-error-invalid-option = Неверная опция

## Команда ListVotesCommand

list-votes-command-description = Перечисляет активные в данный момент голоса
list-votes-command-help = Использование: listvotes

## CancelVoteCommand

cancel-vote-command-description = Отмена активного голосования
cancel-vote-command-help = Использование: cancelvote <id>
                           ID можно получить из команды listvotes.
cancel-vote-command-on-execute-error-invalid-vote-id = Неверный идентификатор голосования
cancel-vote-command-on-execute-error-missing-vote-id = Отсутствующий ID
