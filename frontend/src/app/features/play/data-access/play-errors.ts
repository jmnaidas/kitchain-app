import { HttpErrorResponse } from '@angular/common/http';

const conflicts: Record<string, string> = {
  'Only an Active session can be scored.':
    'This session is not active. Refresh to see its current state.',
  'The score cannot be increased further. Correct the score.':
    'Correct the score before recording another point.',
  'This game is no longer active.':
    'This game has already finished or changed. The room will refresh.',
  'Only an Active session can finish a game.':
    'This session is not active. Refresh to see its current state.',
  'This session has reached its player limit.':
    'This session is full. Resting players still count toward the player limit.',
  'A player with this name is already in the session. Use a distinct name.':
    'That name is already on the roster. Add a surname or another detail to tell players apart.',
  'This session has ended.': 'This session has ended. You can still view the roster.',
  'Only a Draft session can be started.':
    'This session has already started or ended. Refresh to see its current status.',
  'Only a Waiting player can take a break.':
    'This player is no longer waiting. Refresh to see their current state.',
  'Only a Resting player can rejoin the queue.':
    'This player is no longer resting. Refresh to see their current state.',
  'A join code could not be allocated. Please try creating the session again.':
    'We couldn’t reserve a session code. Please try creating your session again.',
};
const fieldMessages: Record<string, string> = {
  name: 'Enter a session name of 1–200 characters.',
  date: 'Enter a valid session date.',
  sessionDate: 'Enter a valid session date.',
  startTime: 'Enter a valid start time.',
  endTime: 'End time must be after start time on the same day.',
  numberOfCourts: 'Enter a whole number of courts, from 1 to 2,147,483,647.',
  maximumPlayers: 'Enter a positive whole number, or leave this blank.',
  displayName: 'Enter a player name of 1–80 characters.',
};

/** Translate known API problems into product copy; never render arbitrary server payloads. */
export function playIssue(error: HttpErrorResponse, context: 'create' | 'join' | 'change') {
  const fields: Record<string, string> = {};
  if (error.status === 400 || error.status === 422) {
    const errors: unknown = error.error?.errors;
    if (errors && typeof errors === 'object') {
      for (const key of Object.keys(errors)) {
        const field = Object.keys(fieldMessages).find(
          (name) => name.toLowerCase() === key.replace(/^\$\./, '').toLowerCase(),
        );
        if (field) fields[field === 'sessionDate' ? 'date' : field] = fieldMessages[field];
      }
    }
    return { fields, message: 'Check your details and try again.' };
  }
  if (error.status === 404)
    return {
      fields,
      message:
        context === 'join'
          ? 'Session not found. Check the six-character code and try again.'
          : 'That session or player is no longer available. Refresh the room.',
    };
  if (error.status === 409) {
    const title: unknown = error.error?.title;
    return {
      fields,
      message:
        typeof title === 'string' && conflicts[title]
          ? conflicts[title]
          : 'The session has changed. Refresh to see its current state.',
    };
  }
  return {
    fields,
    message:
      context === 'change'
        ? 'We couldn’t confirm that change. Refresh the room before trying again.'
        : context === 'join'
          ? 'We couldn’t reach the session. Your code is still here; please try again.'
          : 'We couldn’t confirm session creation. Check your connection before trying again.',
  };
}
