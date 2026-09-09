import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { PlayMatch, PlayScoreCorrection } from '../data-access/play.models';

@Component({
  selector: 'app-play-score-editor',
  templateUrl: './play-score-editor.html',
  styleUrl: './play-score-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayScoreEditor {
  readonly match = input.required<PlayMatch>();
  readonly disabled = input(false);
  readonly save = output<PlayScoreCorrection>();
  readonly cancelEdit = output<void>();
  protected readonly error = signal('');

  protected submit(event: Event) {
    event.preventDefault();
    if (this.disabled()) return;

    const form = event.target as HTMLFormElement;

    if (!form.checkValidity()) {
      this.error.set('Use non-negative whole scores, serving team A or B, and server 1 or 2.');
      form.querySelector<HTMLElement>(':invalid')?.focus();
      return;
    }

    const data = new FormData(form);
    const servingTeam = data.get('servingTeam');

    if (servingTeam !== 'A' && servingTeam !== 'B') return;

    this.error.set('');

    this.save.emit({
      teamAScore: Number(data.get('teamAScore')),
      teamBScore: Number(data.get('teamBScore')),
      servingTeam,
      currentServerNumber: Number(data.get('currentServerNumber')),
    });
  }
}
