## CreateVoteCommand

create-vote-command-description = Создаёт голосование
create-vote-command-help = Использование: createvote <'restart'|'preset'>
create-vote-command-cannot-call-vote-now = Вы не можете начать голосование в данный момент!
create-vote-command-invalid-vote-type = Вы не можете начать голосование в данный момент!

## CreateCustomCommand

create-custom-command-description = Создаёт настраиваемое голосование
create-custom-command-help = customvote <title> <option1> <option2> [option3...]
create-custom-command-on-finished-tie = Ничья {$ties}!
create-custom-command-on-finished-win = {$winner} побеждает!

## VoteCommand

vote-command-description = Голоса при активном голосовании
vote-command-help = vote <voteId> <option>
vote-command-cannot-call-vote-now = Вы не можете начать голосование в данный момент!
vote-command-on-execute-error-must-be-player = Должен быть игроком
vote-command-on-execute-error-invalid-vote-id = Неверное ID голосования
vote-command-on-execute-error-invalid-vote-options = Неверные варианты голосования
vote-command-on-execute-error-invalid-vote = Неверное голосование
vote-command-on-execute-error-invalid-option = Неверное действие

## ListVotesCommand

list-votes-command-description = Перечисляет текущие активные голоса
list-votes-command-help = Использование: listvotes

## CancelVoteCommand

cancel-vote-command-description = Прекращает существующее голосование
cancel-vote-command-help = Использование: cancelvote <id>
                           You can get the ID from the listvotes command.
cancel-vote-command-on-execute-error-invalid-vote-id = Invalid vote ID
cancel-vote-command-on-execute-error-missing-vote-id = Missing ID
