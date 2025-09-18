comp-kitchen-spike-deny-collect = { CAPITALIZE($this) } уже чем-то занят, сначала закончите срезать мясо!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE(THE($victim)) } начинает затаскивать { REFLEXIVE($victim) } на { THE($hook) }!
comp-kitchen-spike-begin-hook-other-self = Вы начинаете затаскивать { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE(THE($user)) } начинает затаскивать { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-hook-self = Вы насадили себя на { THE($hook) }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE(THE($victim)) } насадил { REFLEXIVE($victim) } на { THE($hook) }!
comp-kitchen-spike-hook-other-self = Вы насадили { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-hook-other = { CAPITALIZE(THE($user)) } насадил { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-begin-unhook-self = Вы начинаете стаскивать себя с { THE($hook) }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE(THE($victim)) } начинает стаскивать { REFLEXIVE($victim) } с { THE($hook) }!
comp-kitchen-spike-begin-unhook-other-self = Вы начинаете стаскивать { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE(THE($user)) } начинает стаскивать { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-unhook-self = Вы сняли себя с { THE($hook) }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE(THE($victim)) } снял { REFLEXIVE($victim) } с { THE($hook) }!
comp-kitchen-spike-unhook-other-self = Вы сняли { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-unhook-other = { CAPITALIZE(THE($user)) } снял { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-begin-butcher-self = Вы начинаете разделывать { THE($victim) }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE(THE($user)) } начинает разделывать { THE($victim) }!
comp-kitchen-spike-butcher-self = Вы разделали { THE($victim) }!
comp-kitchen-spike-butcher = { CAPITALIZE(THE($user)) } разделал { THE($victim) }!
comp-kitchen-spike-unhook-verb = Снять
comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE(THE($victim)) } на этом крюке![/color]
comp-kitchen-spike-deny-butcher = { CAPITALIZE($victim) } не может быть разделан на { $this }.
comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } выглядит довольно тощим.[/color]
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE($victim) } не может быть разделан на { $this }, используйте нож для разделки.
comp-kitchen-spike-deny-not-dead =
    { CAPITALIZE($victim) } не может быть разделан. { CAPITALIZE(SUBJECT($victim)) } { GENDER($victim) ->
        [male] ещё жив
        [female] ещё жива
        [epicene] ещё живы
       *[neuter] ещё живо
    }!
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE($user) } начинает насаживать вас на { $this }!
comp-kitchen-spike-begin-hook-self = Вы начинаете насаживать себя на { $this }!
comp-kitchen-spike-kill = { CAPITALIZE($user) } насаживает { $victim } на { $this }, мгновенно убивая { SUBJECT($victim) }!
comp-kitchen-spike-suicide-other = { CAPITALIZE($victim) } бросается на { $this }!
comp-kitchen-spike-suicide-self = Вы бросаетесь на { $this }!
comp-kitchen-spike-knife-needed = Вам нужен нож для этого.
comp-kitchen-spike-remove-meat = Вы срезаете немного мяса с { $victim }.
comp-kitchen-spike-remove-meat-last = Вы срезаете последний кусок мяса с { $victim }!
comp-kitchen-spike-meat-name = мясо { $victim }
